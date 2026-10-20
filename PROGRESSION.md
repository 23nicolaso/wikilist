# Nearer progression

## Current state

The Unity game includes the culture collection, immediate reveal, persistent magnet orbits, and the result panel. The browser build has been reviewed in Edge.

## Milestones

- [x] Preserve the original Listwise prototype and its data checks.
- [x] Create the Unity 6 project and pinned editor integration.
- [x] Implement prose-link extraction, redirect reconciliation, and revision sources.
- [x] Keep seven reference boards for scoring verification.
- [x] Build a playable collection of 106 recognizable culture topics across six editorial areas.
- [x] Bundle 80 attributed subject images with a simple fallback for other topics.
- [x] Support drag, click placement, keyboard movement, and saved progress.
- [x] Make Reveal available immediately, including for untouched rounds.
- [x] Show a faint orbit for each distance and highlight the selected topic's orbit.
- [x] Restore shared-link counts, evidence, and source links in the result panel.
- [x] Keep one continuous Reveal and Next flow with Share.
- [x] Blend five palettes and support reduced motion and an image toggle.
- [x] Separate crowded topics without changing their scored distance.
- [x] Build the browser game.
- [x] Verify names, pictures, palettes, and saved state across round transitions in Edge.

## Design decisions

- One fixed center and five movable topics. Only radial order affects scoring.
- One shared first question each UTC day, followed by a generated sequence.
- Six interests rotate internally without category menus.
- At least four choices share a context with the center.
- An occasional wildcard requires substantial useful overlap and a strong relative connection.
- Topic choice prioritizes recognition and understandable comparisons.
- The source collection is finite, versioned, and expandable.
- There is no countdown and no requirement to touch every topic.
- Equal orbits share one stroke. Fixed decorative rings are omitted.
- The result panel sits on the right on desktop and below the field on narrow screens.
- Selecting evidence leaves the map in place.

## Verification record

- Original Listwise gameplay and artifact checks pass.
- Unity verifies reference scoring, ties, equal-radius guesses, corrupt states, and UTC boundaries.
- Untouched revealed rounds remain valid saved states.
- The culture collection has 106 qualifying centers across six interests.
- The 1,000-question test produced 998 distinct combinations and 82 supported wildcards.
- Every generated test question has at least four related choices and six informative comparisons.
- Reference placements score correctly for all generated test questions.
- A crowded-magnet fixture preserves distance and separates topic bounds.
- Edge checks passed immediate reveal, restoration after reload, topic selection, and result copying.
- The desktop and narrow result compositions were inspected at 1440 by 900 and 360 by 800.
- Round transitions update topic labels and images together, with no browser errors in the reviewed loop.
- Game captures are stored in `Documentation/Captures`.
