# Neardle

A spatial knowledge game. Place stronger connections nearer.

Open the `Nearer` folder in Unity 6.2. Use the Nearer menu to prepare the project, verify the rules and data, or build the browser game. The editor integration package is pinned in the project manifest.

Neardle gives each topic its own compact horizontal number line, with a larger photo on the line and a fixed, readable name beside it. Move stronger connections left and weaker connections right. Fresh rounds start with every photo at the midpoint. Names sit above their own rulers, and the rulers use the full browser width. Drag photos, select one and click the ruler, or use Tab and the arrow keys. Minor ticks mark every 2 placement units. Only the bottom line is numbered, every 10 units. The 0-100 scale is relative distance based on shared-link similarity. The five rows fill the available viewport without page scrolling. Reveal is available immediately; the result panel shows each topic's shared-link count and three examples. On narrow screens, Connections opens those summaries in a dialog.

Reveal counts out a `+x points` award from top to bottom. Photos stay at the player's guesses throughout and after the reveal. Each green target circle and dotted connector appears alongside that topic's points during the one-by-one reveal. Each correct pairwise comparison splits its points between its two topics; the five rounded awards add up to the round's score. The Connections dialog also retains article paths. Paths use breadth-first search over the frozen collection's outgoing links; unavailable routes are marked. These are shortest routes within the bundled graph, not all of Wikipedia.

The browser interface uses persistent native HTML images, accessible draggable sliders, CSS target circles, and SVG dotted connectors. Unity generates questions, stores progress, and scores the round through a JSON bridge, but creates no UI Toolkit document in Web builds. Its off-screen one-pixel canvas runs the game logic. The Unity Editor preview also uses the separate rulers, stationary photos, target circles, and ruler ticks. Existing saved guesses retain their scores. Completed rounds are recorded once per puzzle in this browser. After the five reveals, a results popup shows the current score, best score, average, rounds played, and recent-score bars. Stats reopens it. Reloads never duplicate a result. Run `npm run check:nearer:ui` to check dragging, keyboard controls, stationary reveal positions, ticks, image-node stability, sources, and round reset.

Questions use a culture-focused collection of familiar brands, actors, films, performers, games, and athletes. At least four choices share a context with the center. Occasional wildcards need a substantial real connection. Each sequence rotates across six interests without category menus. The first question is shared each day in UTC. The source collection is finite and frozen with revision links. See `Documentation/DATA.md` for selection rules and the extraction method.

After building, run `npm run nearer` and open the local address printed by the server in Edge. The output is in `Nearer/Builds/Web`. Serve it over HTTP rather than opening the page as a local file.

Run `npm run check:nearer` for game data checks. Use Nearer > Verify rules and data in Unity for scoring, generation, and magnet spacing tests. Run `npm run data:nearer` when intentionally refreshing the source collection. Run `npm run media:nearer` to obtain freely licensed subject images and their credits.

The implementation brief is in `PROMPT.md`. Current milestones and verified results are in `PROGRESSION.md`.

## Original Listwise prototype

A daily, three-minute Wikipedia table guessing game. The Cloudflare Worker discovers list articles through MediaWiki search and proxies article HTML. The browser selects a suitable table column, deduplicates entries, and runs the round. Daily selection is date-seeded in UTC; practice uses a fresh seed. No fixed puzzle schedule or paid API is required.

Daily and practice rounds allow niche, country-specific, regional, and specialist topics. The only popularity requirement is a minimum median of **50 daily English Wikipedia views** for the list article over the past 30 days, with at least 14 measured days. Individual answers do not need to be popular. Extraction still requires 12?250 unique entries in a usable table column. Missing popularity data rejects a puzzle; API outages show a retry message. Requests are throttled, retried on rate limits, and cached for an hour. Backups use the same popularity check, and versioned storage replaces puzzles selected under the old filters.

The threshold is configurable as `MIN_DAILY_VIEWS` in `worker/index.js`. Run `node scripts/check-game.mjs --live` to check real discovery, extraction, and screening as well as the offline gameplay tests.

With Node 20 or newer, run `npm run dev` and open http://localhost:3000. No install or API keys needed. Run `npm test` for gameplay checks and `npm run build` to create the deployment artifact. The Worker exports `default.fetch` and can also run with a Cloudflare-compatible local development server.

Progress and the selected puzzle are saved in browser localStorage. The timer uses an absolute deadline so refreshes and background tabs do not pause it. Clearing browser data resets progress. This is a casual client-side game: answers are available in browser memory. Wikipedia search results and tables can change, and automatic extraction is heuristic. It includes all unique entries in the selected table column, not necessarily every table in the article. When discovery fails, it tries a small set of known list articles; if Wikipedia is unavailable it shows a retry action.

Wikipedia-derived text is attributed in the UI under CC BY-SA 4.0, with an article revision link after each round.
