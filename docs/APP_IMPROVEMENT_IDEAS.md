# Nigerian News Grid - Improvement Ideas

## Feature ideas
- [ ] Personalized briefings by category and reading history.
- [-] Multi-language summaries and voice-over in Yoruba, Igbo, and Hausa.
- [ ] Smart audio playlist that combines the top 3 stories from each category into a 5-minute briefing.
- [ ] Offline-first support: download the daily briefing over Wi-Fi.
- [x] User accounts with saved topics, notifications, and watchlists.
- [-] Sentiment and fact-check badges for controversial stories.
- [ ] Topic-based alerts for breaking news and specific keywords (e.g. "fuel price", "elections").

## Product improvements
- [ ] Add a home screen card for the "Top 3 headlines of the day".
- [ ] Show source credibility and recency tags for each article.
- [ ] Introduce a premium tier for ad-free audio and personalized digests.
- [ ] Add analytics for open rates, listening minutes, and most-read categories.

## Official X (Twitter) Syndication & Community Discussion (Phase 1)
> **Status**: Ready for implementation as soon as the official X account & Developer API credentials (`ApiKey`, `ApiSecret`, `AccessToken`, `AccessTokenSecret`) are created.

### Overview
Automatically syndicate curated top/breaking news stories from `NewsScraperService` to the official Nigerian News Grid X handle, and link each story's discussion directly into the mobile app to tap into Nigerian Twitter's active public square without incurring moderation liability or high API costs.

### Architecture & Components
1. **Official X Handle & Developer Setup (Pending)**:
   - Create official handle (e.g. `@NewsGridNG` / `@NigerianNewsGrid`).
   - Register under X Developer Free Tier (1,500 tweets/month write-only ceiling, ~50 tweets/day).
   - Generate OAuth 1.0a User Context credentials (`ApiKey`, `ApiSecret`, `AccessToken`, `AccessTokenSecret`).

2. **Backend Data Model (`Articles` Table)**:
   - Add `TwitterPostId` (`string?`): Stores the returned Tweet ID (e.g., `"1836102938475..."`).
   - Add `TwitterPostUrl` (`string?`): Canonical web URL (`https://x.com/YourHandle/status/{id}`).
   - Add `TweetedAt` (`DateTime?`): Timestamp of publication.

3. **Background Auto-Publisher Worker (`TwitterPublisherWorker`)**:
   - Runs as a hosted service on a 30–45 minute timer (posting 1–2 high-priority stories/hour to stay comfortably within the 1,500/month free quota).
   - Curation logic: Prioritizes breaking news, high ML confidence scores, and category diversity (politics, business, sports, tech).
   - Anti-spam & human-style formatting: Headline + hook + relevant Nigerian hashtags (`#Nigeria`, `#NaijaNews`, `#PoliticsNG`) + article link.

4. **.NET MAUI Mobile App Integration**:
   - **"Discuss on X" Pill Button**: Displays prominently under the article in `ArticleWebPage.xaml` / reader view when `TwitterPostId` is populated.
   - **Native Deep-Linking**:
     - Attempts `twitter://status?id={TwitterPostId}` to launch the native X app directly into the reply thread.
     - Gracefully falls back to system browser (`BrowserLaunchMode.SystemPreferred`) if the native app is not installed.
   - **Fallback Share CTA**: If an article has not been auto-tweeted yet, provides a 1-tap "Share & Start Discussion on X" button with pre-populated text and URL.

