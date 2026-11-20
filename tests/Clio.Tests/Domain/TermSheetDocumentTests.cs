using Clio.Domain;
using Xunit;

namespace Clio.Domain.Tests;

public class TermSheetDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly ConfidenceThreshold Threshold = new(0.90m);

    private static TermSheetDocument NewDoc()
    {
        var doc = TermSheetDocument.Create(Guid.NewGuid(), "ts.txt", "text/plain", 100, "abc", "user-1", Now);
        Assert.True(doc.StartProcessing().IsSuccess);
        return doc;
    }

    private static FieldCandidate[] Candidates(decimal marginConfidence) =>
    [
        new(TermField.LoanAmount, "ZAR 48,500,000", 0.97m, new SourceLocation(1, 7)),
        new(TermField.Margin, "2.85", marginConfidence, new SourceLocation(1, 12)),
        new(TermField.Maturity, "2031-06-30", 0.96m, new SourceLocation(2, 3)),
    ];

    [Fact]
    public void All_fields_above_threshold_are_auto_accepted_and_document_is_complete()
    {
        var doc = NewDoc();
        Assert.True(doc.RecordExtraction(Candidates(0.95m), Threshold, "rules-v1", Today).IsSuccess);
        Assert.Equal(DocumentStatus.Complete, doc.Status);
        Assert.All(doc.Fields, f => Assert.Equal(FieldStatus.AutoAccepted, f.Status));
        Assert.Equal("ZAR 48500000.00", doc.SavedTerms[TermField.LoanAmount]);
    }

    [Fact]
    public void Field_below_threshold_needs_review_and_is_not_saved()
    {
        var doc = NewDoc();
        doc.RecordExtraction(Candidates(0.72m), Threshold, "rules-v1", Today);
        Assert.Equal(DocumentStatus.NeedsReview, doc.Status);
        Assert.Equal(FieldStatus.NeedsReview, doc.FieldOf(TermField.Margin)!.Status);
        Assert.False(doc.SavedTerms.ContainsKey(TermField.Margin));
        Assert.Equal(2, doc.SavedTerms.Count);
    }

    [Fact]
    public void Approving_stores_approver_time_and_keeps_source_then_completes()
    {
        var doc = NewDoc();
        doc.RecordExtraction(Candidates(0.72m), Threshold, "rules-v1", Today);
        var approved = doc.Approve(TermField.Margin, "2.90", "user-7", Now.AddMinutes(3));
        Assert.True(approved.IsSuccess);
        Assert.Equal("2.90", approved.Value.Value);
        Assert.Equal("user-7", approved.Value.ApprovedBy);
        Assert.Equal(Now.AddMinutes(3), approved.Value.ApprovedAt);
        Assert.Equal(new SourceLocation(1, 12), approved.Value.Source);
        Assert.Equal(DocumentStatus.Complete, doc.Status);
    }

    [Fact]
    public void Approving_twice_with_the_same_value_is_idempotent_but_a_different_value_conflicts()
    {
        var doc = NewDoc();
        doc.RecordExtraction(Candidates(0.72m), Threshold, "rules-v1", Today);
        doc.Approve(TermField.Margin, null, "user-7", Now);
        Assert.True(doc.Approve(TermField.Margin, "2.85", "user-8", Now).IsSuccess);
        Assert.Equal("user-7", doc.FieldOf(TermField.Margin)!.ApprovedBy);
        Assert.False(doc.Approve(TermField.Margin, "3.00", "user-8", Now).IsSuccess);
    }

    [Fact]
    public void Approving_without_a_value_when_none_was_found_is_rejected()
    {
        var doc = NewDoc();
        doc.RecordExtraction([new FieldCandidate(TermField.Maturity, null, 0m, null)], Threshold, "rules-v1", Today);
        Assert.False(doc.Approve(TermField.Maturity, null, "user-7", Now).IsSuccess);
        Assert.True(doc.Approve(TermField.Maturity, "2030-01-31", "user-7", Now).IsSuccess);
    }

    [Fact]
    public void Model_output_that_fails_validation_goes_to_review_with_no_value()
    {
        var doc = NewDoc();
        doc.RecordExtraction([new FieldCandidate(TermField.Margin, "lots", 0.99m, new SourceLocation(1, 1))], Threshold, "rules-v1", Today);
        var margin = doc.FieldOf(TermField.Margin)!;
        Assert.Equal(FieldStatus.NeedsReview, margin.Status);
        Assert.Null(margin.Value);
        Assert.Equal(0m, margin.Confidence);
    }

    [Fact]
    public void Unreadable_document_keeps_a_clear_error()
    {
        var doc = NewDoc();
        doc.MarkUnreadable("document.unreadable", "No text layer.");
        Assert.Equal(DocumentStatus.Unreadable, doc.Status);
        Assert.Equal("document.unreadable", doc.ErrorCode);
        Assert.Empty(doc.Fields);
    }

    [Fact]
    public void Threshold_must_be_between_zero_and_one() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConfidenceThreshold(1.5m));
}
