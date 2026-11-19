# Backlog

Vertical slices, smallest valuable first.

## Now
- [ ] Slice 1: Analyst uploads one term sheet and sees extracted loan amount, margin and maturity, each with confidence and a link to the page
  - Given a clear term sheet, when it is uploaded, then amount, margin and maturity are extracted with confidence >= 0.9
  - Given a field below the threshold, then it is shown in the review queue, not saved
  - Given an approved field, then its source page and the approving user are stored
  - Given an unreadable file, then the analyst sees a clear error and the original is kept
  - Test notes: one acceptance test per criterion; seeded invented data.

## Next
- [ ] Sketch leg: tokens + three variants of the slice-1 screen (`sketch-ui`)
- [ ] CI green with all gates before the first feature commit

## Later
