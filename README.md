# Listwise

A daily, three-minute Wikipedia table guessing game. The Cloudflare Worker discovers list articles through MediaWiki search and proxies article HTML. The browser selects a suitable table column, deduplicates entries, and runs the round. Daily selection is date-seeded in UTC; practice uses a fresh seed. No fixed puzzle schedule or paid API is required.

Daily and practice rounds allow niche, country-specific, regional, and specialist topics. The only popularity requirement is a minimum median of **50 daily English Wikipedia views** for the list article over the past 30 days, with at least 14 measured days. Individual answers do not need to be popular. Extraction still requires 12?250 unique entries in a usable table column. Missing popularity data rejects a puzzle; API outages show a retry message. Requests are throttled, retried on rate limits, and cached for an hour. Backups use the same popularity check, and versioned storage replaces puzzles selected under the old filters.

The threshold is configurable as `MIN_DAILY_VIEWS` in `worker/index.js`. Run `node scripts/check-game.mjs --live` to check real discovery, extraction, and screening as well as the offline gameplay tests.

With Node 20 or newer, run `npm run dev` and open http://localhost:3000. No install or API keys needed. Run `npm test` for gameplay checks and `npm run build` to create the deployment artifact. The Worker exports `default.fetch` and can also run with a Cloudflare-compatible local development server.

Progress and the selected puzzle are saved in browser localStorage. The timer uses an absolute deadline so refreshes and background tabs do not pause it. Clearing browser data resets progress. This is a casual client-side game: answers are available in browser memory. Wikipedia search results and tables can change, and automatic extraction is heuristic. It includes all unique entries in the selected table column, not necessarily every table in the article. When discovery fails, it tries a small set of known list articles; if Wikipedia is unavailable it shows a retry action.

Wikipedia-derived text is attributed in the UI under CC BY-SA 4.0, with an article revision link after each round.
