# Connection data

Nearer compares the sets of article links found in Wikipedia prose. Each set contains unique, existing article targets after redirects have been resolved. Tables, reference lists, navigation boxes, sidebars, and other non-prose content are excluded.

The culture importer reads existing-link and redirect classifications from the saved rendered article. Missing links are excluded. Redirect targets are reconciled through the API in batches, while ordinary existing links retain their rendered titles. MediaWiki documents the `mw-redirect` marker in its [redirect guide](https://www.mediawiki.org/wiki/Help:Redirects/en#Links_to_redirect). This avoids downloading unrelated navigation links merely to resolve a prose target.

The connection value is the size of the intersection divided by the size of the union. This is Jaccard overlap. It describes shared linked subject matter in a particular pair of saved articles. It is not a statement about the absolute meaning of two concepts.

The five topics are ordered by this value. Pairs whose values differ by no more than eight percent of the board's maximum connection value are excluded from the score. A board must retain at least six informative pairs. The score is the percentage of remaining pairwise ordering decisions the player gets right. Player placements at effectively equal radii do not count as a correct ordering for unequal data.

The reference positions show rank rather than absolute overlap magnitude. They are illustrative radial positions, not physical coordinates or geographic distances. Topic angles have no effect on scoring.

Seven historical reference boards remain in `Nearer/Assets/Resources/nearer-puzzles.json` for scoring verification. Playable questions use a separate culture collection in `Nearer/Assets/Resources/nearer-catalog.json`. The editorial source list is `scripts/nearer-topics.json`: food brands, screen culture, performers, games, internet brands, and sport. These are selection constraints rather than player-facing categories. Related areas can overlap, such as music platforms and performers or sports brands and athletes.

A center needs at least seven related candidates. A candidate needs at least three shared prose targets beyond generic country, language, and newspaper names. At least four of the five choices share an editorial context with the center. Every fourth question may use one outside-context wildcard if it shares at least eight useful targets and its overlap value meets the median of the related pool. If no such choice exists, the question stays within context. The other choices vary in strength. A question needs at least six informative comparisons and a meaningful spread of connection values.

Each block visits all six editorial areas in a shuffled order. Centers rotate within each area before repeating. The first question uses the UTC date as a shared seed. The collection is editorially chosen for recognition and variety; it does not claim to measure live trends or personal interests.

Generation is reproducible from the date, question number, catalog version, and generator version. The current source collection has finite combinations. New source articles expand the pool. A generator test samples 1,000 questions and measures actual distinct combinations rather than counting shuffled visual positions as new content.

The interface can move a magnet around its circle to prevent label overlap. This angular adjustment preserves the represented distance and does not alter the score. Inner display spacing protects the central image; the visible distance remains monotonic with the player's ordering.

Run `npm run data:nearer` to rebuild the playable collection from public Wikipedia articles. The importer caches responses, reconciles linked targets in bounded batches, and honors service retry intervals. Regeneration is an editorial update rather than part of a player's round. Review sample questions before distributing it. `scripts/generate-nearer.mjs` retains the historical reference-board importer.

The right-hand result panel displays the exact shared-link count and example targets for the selected topic. Distance is ordered by overlap adjusted for article size, so a larger raw count does not always mean a closer connection. The source view explains this distinction. Reveal accepts untouched or partially arranged rounds, and those results can resume after a reload.

Wikipedia-derived text is attributed to Wikipedia contributors under CC BY-SA 4.0. The application links to the exact source article revisions and the license. Subject thumbnails are bundled with their authors, source pages, and licenses in `Nearer/Assets/Resources/nearer-media.json`; the source view exposes credits for the current question. The bundled Liberation Sans font uses the SIL Open Font License; its license is included alongside the font.
