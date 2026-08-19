# Nigerian News Grid - Legal, Privacy, and Safety Notes

## Scraping policy
- Prefer RSS feeds and official APIs over raw HTML scraping.
- Respect each source's robots.txt and Terms of Service.
- Add per-source rate limits and backoff to avoid overloading publishers.
- Keep attribution fields on all articles and audio outputs.

## Privacy
- The mobile app stores user preferences locally (language, bookmarks, reminder opt-in).
- Do not collect sensitive personal data in the MVP.
- Add a privacy policy and consent screen before collecting analytics or push tokens in production.

## Content licensing
- Do not republish full copyrighted content beyond short summaries and links.
- Keep article links back to the original publisher.
- Track source licenses and remove content if requested by the rights holder.
