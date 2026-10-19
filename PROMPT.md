# Nearer

Build a spatial knowledge game in Unity 6 for desktop browsers, with Microsoft Edge as the primary acceptance browser.

## The game

Place five topics around a central topic. Stronger connections belong closer. Reveal the arrangement to see what Wikipedia's links say.

One topic remains fixed at the center. Five topic labels can be placed freely across a circular field. Only their distance from the center affects the score. Angle is free. Reveal is available from the beginning, including when every topic is untouched. There is no countdown.

Score the ten pairwise ordering decisions between the five movable topics. Compare normalized overlap between the articles' prose link targets. Treat sufficiently similar relationships as ties. Equal player distances must not bypass an unequal relationship. The result is a score from 0 to 100.

Keep the original placements visible as faint marks during the reveal. Move each topic radially to its reference order. Explain connections with actual shared Wikipedia links. Keep the explanation available after the motion ends.

Restore a quiet panel on the right after revealing. Selecting any topic updates its shared-link count, example links, and article source. Show one topic by default. On smaller screens, place the same information below the field. Keep the graph stable while selection changes. Keep Share beside Next.

## Data

Use a single sequence of generated questions. The main action is Reveal, followed by Next. Do not expose category, practice, or archive menus. The first question is shared each day in UTC. Subsequent questions draw new combinations from a frozen source catalog. Keep at least seven reference boards for data verification. Preserve article revision identifiers, extraction rules, overlap counts, and evidence links. Answers must not change when an article is edited.

Use public Wikipedia data to generate candidates and calculate answers. Exclude navigation templates, references, tables, and other non-prose links. Reconcile redirects before scoring. Review generated boards for recognizable topics, sufficient overlap, clear comparisons, and understandable evidence. Retain the source and license attribution in a quiet source view.

Build the playable collection around recognizable performers, films, brands, games, internet culture, and athletes. Rotate across these interests without adding category menus. At least four choices in every round must share an editorial context with the center. A wildcard is occasional and must have substantial useful shared links and a connection value at least as strong as the related pool's median. Do not use arbitrary unrelated filler to manufacture difficulty. Familiar subjects and explainable surprises matter more than a theoretical combination count.

## Visual direction

The board is the composition. It fills the available viewport. Use generous space, precise typography, fine geometry, and deliberate motion. Text supports the interaction and remains brief.

Use a sequence of soft palettes:

- Tide: #E5EEF1, #F2EFE8, #386378
- Lilac: #ECE8F1, #E7EFF0, #69558A
- Clay: #F2E8E5, #F0E9F1, #8B5263
- Sage: #E8EEE3, #E4EEF0, #476C57
- Blue: #E5EBF4, #F0ECE9, #49658B

Blend the background and accent smoothly when advancing. Keep backgrounds quiet and text dark. Use gradients as soft atmosphere rather than decoration. Respect reduced motion.

Use Helvetica where available in the browser. Bundle an openly distributable neo-grotesque sans-serif with an SDF font asset for crisp Unity text. Labels should be readable at normal browser zoom without becoming headlines. Center button text explicitly. Match the Game View to the available display instead of enlarging a small fixed render.

The central topic has a clear visual anchor. Use circular, attributed subject images and unboxed labels. Offer an image toggle. Every magnet has a persistent, very faint orbit at its current distance. Selecting a magnet darkens its orbit, before and after revealing. Do not draw fixed decorative rings. Overlapping orbits at the same radius share one stroke. Center orbits on the subject images. Keep topic bounds apart by adjusting their angles while preserving their scored distance. The reveal preserves a ghost of the player's arrangement and traces the correction.

Avoid card grids, decorative badges, oversized headings, marketing copy, arbitrary statistics, heavy shadows, neon, and constant ambient motion. Player-facing copy uses ordinary short phrases. Do not use em dashes or slash separators in interface text.

## Interaction

Support mouse, touch, keyboard, and a click-to-place alternative. Make focus visible. Provide a reduced-motion setting. Selecting a topic should make the relevant controls and evidence clear without adding permanent clutter. Keep controls stable between placement and reveal.

Persist the question number and unfinished or completed arrangement. Refreshing must preserve both. Changing viewport size must preserve radial ordering. Recover gracefully from unavailable browser storage and clipboard access. Keep photo credits accessible in the source view.

## Project structure

- `Nearer` contains the Unity project.
- `scripts` contains data generation, serving, and verification utilities.
- `worker` contains the original Listwise prototype.
- `Documentation` contains source notes and selected game captures.

Keep Listwise's authored source intact. The new game owns its data and build pipeline.

## Acceptance

Verify scoring, ties, persistence, question generation, source evidence, image credits, and input handling. Stress-test crowded magnet arrangements and confirm that angular spacing preserves distance. Compile in Unity 6, inspect Play Mode, produce a Web build, and test the actual build in Edge. Inspect the composition at multiple desktop sizes and a narrow viewport. Check placement, reveal, reload, Next, palette transitions, image toggling, reduced motion, and keyboard controls.

Review rendered output before calling the work complete. Judge each element by its purpose, scale, spacing, and relationship to the entire screen. Revise anything that feels generic or visually unresolved.
