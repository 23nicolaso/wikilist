# Neardle

A spatial knowledge game. Place stronger connections nearer.

Open the `Nearer` folder in Unity 6.2. Use the Nearer menu to prepare the project, verify the rules and data, or build the browser game. The editor integration package is pinned in the project manifest.

Neardle restores the original circular layout from c15212b. Drag stronger connections toward the central reference and weaker connections farther away. Native HTML portraits stay stable during dragging and reveals. Keyboard users can use the arrow keys, with Shift for larger steps and Home/End for the minimum/maximum radius.

Original scoring counts each visible distance whether or not the circle was dragged: each correctly ordered comparison earns an equal share of the 100-point total, split between its two topics. Near ties are ignored. The awards add up to the round score. Reveal runs clockwise, displaying each award with a green dashed connector and small green target circle at the correct distance along the topic's current angle. Photos stay at the player's guesses throughout.

The original circular arrangement is rendered in HTML/CSS/SVG instead of Unity's browser UI renderer. Progress is saved separately from the box experiment; personal best, average, and recent scores remain saved in this browser. The completion popup appears only after all five reveals. Shared-link summaries and shortest article paths remain available. Run `npm run check:nearer:ui` to verify dragging, keyboard controls, stationary guesses, progressive corrections, stats, image stability, and round reset.

Each one-by-one topic reveal plays the supplied menu click sound; completion plays the supplied game sound once after the final award. Pressing Reveal prepares audio silently. Audio files live in the WebGL template Sounds folder and load when Reveal is pressed. Next and the result popup Next round button play the supplied Rise07 sound, converted losslessly from AIFF to browser-compatible WAV. Opening Stats or reloading completed rounds stays silent.

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
