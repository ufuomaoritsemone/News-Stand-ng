import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, RefreshCw, Mail, CheckCircle2 } from "lucide-react";

export function meta() {
  const title = "Corrections & Retractions Policy | News Stand NG";
  const description =
    "Our protocol for managing factual corrections, source retractions, and disputed news reports across aggregated publishers.";
  const canonicalUrl = "https://newsstand.ng/corrections";

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

export default function CorrectionsPage() {
  const correctionsJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "Corrections Policy — News Stand NG",
    "url": "https://newsstand.ng/corrections",
    "description": "News Stand NG protocol for corrections, updates, and retractions of aggregated reports.",
    "publisher": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng"
    }
  };

  return (
    <div className="static-page-layout">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(correctionsJsonLd) }}
      />

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
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">Corrections</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Corrections & Retractions Policy</h1>
          <p className="static-policy-subtitle">
            Transparency and accountability: How News Stand NG rectifies inaccuracies, synchronizes publisher retractions, and maintains historical record accuracy.
          </p>

          <div className="static-policy-content">
            <h2>Our Commitment to Truth and Accuracy</h2>
            <p>
              In high-velocity breaking news coverage, facts can develop rapidly. When new information renders an earlier headline incomplete, misleading, or incorrect, our responsibility to Nigerian readers demands prompt, unreserved rectification.
            </p>

            <h2>Automated Synchronization with Primary Sources</h2>
            <p>
              Our continuous ingestion system periodically re-crawls active stories within 48 hours of initial publication:
            </p>
            <ul>
              <li><strong>Headline & Excerpt Updates:</strong> If the original publisher corrects their headline (e.g. updating casualty figures, clarifying names, or adding official rejoinders), our system propagates the updated text within minutes.</li>
              <li><strong>Retracted Articles:</strong> If an original publisher removes or issues a formal retraction notice for an article, our ingestion pipeline unpublishes the corresponding aggregator index or appends a retraction indicator.</li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Reporting an Inaccuracy:</strong> If you identify an erroneous excerpt, broken source attribution, or misattributed quote on News Stand NG, please email our editorial desk directly at <a href="mailto:corrections@newsstand.ng">corrections@newsstand.ng</a> with the story URL and relevant documentation.
            </div>

            <h2>Correction Review Protocol</h2>
            <p>
              When a correction request or complaint is received:
            </p>
            <ol>
              <li><strong>Verification (1–4 Hours):</strong> An editorial reviewer compares our aggregated snippet against the live origin publisher URL and primary source material.</li>
              <li><strong>Immediate Remediation:</strong> If the error originated from our ingestion parser (such as a truncated headline or bad category mapping), it is corrected immediately.</li>
              <li><strong>Publisher Discrepancies:</strong> If the origin publisher published flawed information, we contact the publisher’s newsroom and, where necessary, flag the story as disputed or update our topic hub with rebuttal coverage.</li>
            </ol>

            <h2>Contact Our Corrections Desk</h2>
            <p>
              Email: <a href="mailto:corrections@newsstand.ng">corrections@newsstand.ng</a><br />
              Hours: 24 hours / 7 days a week for urgent legal or defamation notices.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
