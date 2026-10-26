import { index, route } from "@react-router/dev/routes";

export default [
  // Page routes
  index("routes/home.jsx"),
  route("news/:slug", "routes/news.$slug.jsx"),
  route("story/:slug", "routes/story.$slug.jsx"),
  route("topic/:slug", "routes/topic.$slug.jsx"),
  route("category/:category", "routes/category.$category.jsx"),
  route("source/:slug", "routes/source.$slug.jsx"),

  // Phase 8: Evergreen Data Trackers & Regional State Hubs
  route("states", "routes/states.jsx"),
  route("state/:slug", "routes/state.$slug.jsx"),
  route("naira-exchange-rate", "routes/naira-exchange-rate.jsx"),
  route("fuel-price-nigeria", "routes/fuel-price-nigeria.jsx"),
  route("jamb-waec-news", "routes/jamb-waec-news.jsx"),
  route("elections", "routes/elections.jsx"),

  // Phase 8: Nigerian Pidgin (pcm) Localized Routes
  route("pcm/states", "routes/states.jsx", { id: "routes/pcm-states" }),
  route("pcm/state/:slug", "routes/state.$slug.jsx", { id: "routes/pcm-state-slug" }),
  route("pcm/naira-exchange-rate", "routes/naira-exchange-rate.jsx", { id: "routes/pcm-naira-exchange-rate" }),
  route("pcm/fuel-price-nigeria", "routes/fuel-price-nigeria.jsx", { id: "routes/pcm-fuel-price-nigeria" }),
  route("pcm/jamb-waec-news", "routes/jamb-waec-news.jsx", { id: "routes/pcm-jamb-waec-news" }),
  route("pcm/elections", "routes/elections.jsx", { id: "routes/pcm-elections" }),

  // Static Policy, Standards & Trust Pages (Phase 5 E-E-A-T)
  route("about", "routes/about.jsx"),
  route("editorial-policy", "routes/editorial-policy.jsx"),
  route("corrections", "routes/corrections.jsx"),
  route("contact", "routes/contact.jsx"),
  route("privacy", "routes/privacy.jsx"),
  route("terms", "routes/terms.jsx"),
  route("ai-disclosure", "routes/ai-disclosure.jsx"),

  // SEO & Crawler Resource Routes
  route("robots.txt", "routes/robots[.]txt.jsx"),
  route("sitemap.xml", "routes/sitemap[.]xml.jsx"),
  route("sitemap-news.xml", "routes/sitemap-news[.]xml.jsx"),
  route("sitemap-stories.xml", "routes/sitemap-stories[.]xml.jsx"),
  route("sitemap-states.xml", "routes/sitemap-states[.]xml.jsx"),
  route("sitemap-data.xml", "routes/sitemap-data[.]xml.jsx"),
  route("sitemap-articles-:page", "routes/sitemap-articles.$page[.]xml.jsx"),
  route("sitemap-topics.xml", "routes/sitemap-topics[.]xml.jsx"),
  route("sitemap-categories.xml", "routes/sitemap-categories[.]xml.jsx"),
  route("sitemap-sources.xml", "routes/sitemap-sources[.]xml.jsx"),

  // RSS 2.0 Syndication Feeds
  route("feed.xml", "routes/feed[.]xml.jsx"),
  route("category/:category/feed.xml", "routes/category.$category.feed[.]xml.jsx"),

  // IndexNow Protocol Domain Key Verification
  route(":key.txt", "routes/indexnow-key[.]txt.jsx"),

  // API Proxy Route for Docker and production environments
  route("api/*", "routes/api.$.jsx"),
];
