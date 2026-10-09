import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, Shield, Cpu, Newspaper, Users } from "lucide-react";

export function meta() {
  const title = "About News Stand NG — High-Velocity Nigerian News Aggregator";
  const description =
    "Learn about News Stand NG: our mission, technological architecture, publisher attribution pledge, and editorial integrity standards.";
  const canonicalUrl = "https://newsstand.ng/about";

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow" },
    { property: "og:type", content: "website" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
  ];
}

export default function AboutPage() {
  const aboutJsonLd = {
    "@context": "https://schema.org",
    "@type": "AboutPage",
    "name": "About News Stand NG",
    "url": "https://newsstand.ng/about",
    "description": "Information about News Stand NG, its aggregation technology, mission, and publisher partnership principles.",
    "mainEntity": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng",
      "logo": "https://newsstand.ng/logo.png"
    }
  };

  return (
    <div className="static-page-layout">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(aboutJsonLd) }}
      />

      {/* Header */}
      <header className="article-page-header">
        <div className="article-header-container">
          <Link to="/" className="article-header-back-btn" title="Back to Headlines">
            <ArrowLeft size={18} />
            <span>Headlines</span>
          </Link>

          <Link to="/" className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>

          <div style={{ width: 80 }} />
        </div>
      </header>

      <main className="static-policy-container">
        {/* Breadcrumb */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">About Us</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">About News Stand NG</h1>
          <p className="static-policy-subtitle">
            Nigeria's premier real-time news aggregator delivering verified headlines, deep topical indexing, and transparent publisher attribution.
          </p>

          <div className="static-policy-content">
            <h2>Our Mission</h2>
            <p>
              News Stand NG was created to solve information fragmentation across Nigeria's dynamic media ecosystem. With dozens of reputable national dailies, broadcast networks, investigative bureaus, and digital-native newsrooms reporting simultaneously, readers need a single, high-velocity vantage point to stay informed without falling victim to algorithmic echo chambers or unverified misinformation.
            </p>
            <p>
              Our mission is twofold: <strong>empower Nigerian citizens</strong> with immediate, neutral access to breaking news from all 36 states and the Federal Capital Territory, while <strong>actively supporting and championing original publishers</strong> through transparent referral traffic.
            </p>

            <h2>Publisher Attribution Pledge</h2>
            <div className="static-policy-note-box">
              <strong>Aggregator Ethics:</strong> News Stand NG is an aggregator, not an origin publisher. We do not republish complete articles or monetize uncredited journalistic work. Every headline links directly to the originating news organization, ensuring publishers receive 100% of full-page readership, impressions, and ad revenues.
            </div>
            <p>
              Under our fair-use and copyright compliance framework:
            </p>
            <ul>
              <li>We present only short editorial excerpts (maximum 200–300 characters) or structured summaries to provide context.</li>
              <li>Every story card, hero feature, and article page prominently displays the primary source and provides an outbound <code>Read full story ↗</code> link.</li>
              <li>We respect publisher <code>robots.txt</code> directives and respond immediately to any publisher content-handling requests.</li>
            </ul>

            <h2>Technology & Speed</h2>
            <p>
              News Stand NG is built from the ground up on modern, distributed infrastructure engineered for low-bandwidth and high-latency African mobile connectivity:
            </p>
            <ul>
              <li><strong>Distributed RSS & XML Ingestion:</strong> Continuous automated scraping services ingest updates from over 20 verified national feeds in real time.</li>
              <li><strong>BM25 Full-Text & Vector Search:</strong> Advanced relevance indexing allows readers to search millions of archive documents across politics, business, technology, and sports in milliseconds.</li>
              <li><strong>Edge Caching & Core Web Vitals:</strong> Optimized for sub-2.5 second Largest Contentful Paint (LCP) and zero Cumulative Layout Shift (CLS), even on 3G cellular connections.</li>
            </ul>

            <h2>Editorial Team & Presence</h2>
            <p>
              Our engineering, data science, and editorial review desks operate across Lagos and Abuja, collaborating with Nigerian journalists, legal scholars, and media ethicists to maintain our standard of neutrality, trust, and technological excellence.
            </p>
            <p>
              For press inquiries, publisher syndication partnerships, or general feedback, visit our <Link to="/contact">Contact Page</Link>.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
