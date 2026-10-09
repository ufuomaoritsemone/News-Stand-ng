import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, FileText, Scale, ExternalLink } from "lucide-react";

export function meta() {
  const title = "Terms of Service & Aggregator Disclaimer | News Stand NG";
  const description =
    "Review the terms of service, publisher intellectual property disclaimers, and user conduct guidelines for News Stand NG.";
  const canonicalUrl = "https://newsstand.ng/terms";

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

export default function TermsPage() {
  const termsJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "Terms of Service — News Stand NG",
    "url": "https://newsstand.ng/terms",
    "description": "Terms of service and fair use notice for News Stand NG.",
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
        dangerouslySetInnerHTML={{ __html: JSON.stringify(termsJsonLd) }}
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
            <li className="breadcrumb-active" aria-current="page">Terms of Service</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Terms of Service & Aggregator Notice</h1>
          <p className="static-policy-subtitle">
            Please read these terms carefully before accessing or using News Stand NG.
          </p>

          <div className="static-policy-content">
            <h2>1. Nature of Service: News Aggregation & Fair Use</h2>
            <p>
              News Stand NG operates as a digital news aggregator, search indexing utility, and media referral directory.
            </p>
            <div className="static-policy-note-box">
              <strong>Copyright Ownership:</strong> All headline excerpts, article snippets, and publisher trademarks indexed by News Stand NG belong exclusively to their respective copyright owners (including Vanguard Media, Punch Nigeria Limited, Premium Times Services, Daily Post Media, Channels Television, and others). News Stand NG claims no copyright ownership over third-party news copy.
            </div>

            <h2>2. Acceptable Use Policy</h2>
            <p>
              By accessing this service, you agree not to:
            </p>
            <ul>
              <li>Use automated tools to excessively query, scrape, or flood the API in a manner that degrades service availability for Nigerian mobile users.</li>
              <li>Post illegal, abusive, hate-inciting, or defamatory comments in community discussions.</li>
              <li>Attempt to circumvent security tokens, rate limits, or administrative access controls.</li>
            </ul>

            <h2>3. Third-Party Links & Outbound Disclaimers</h2>
            <p>
              News Stand NG contains outbound hyperlinks to external third-party news websites. We have no direct editorial control over the continuing availability, accuracy, or content posted on external third-party domains. Following external hyperlinks is at your own discretion.
            </p>

            <h2>4. Limitation of Liability</h2>
            <p>
              While we implement automated validation and verification filters, news evolves continuously. News Stand NG does not warrant that aggregated headlines are completely error-free or uninterrupted. The service is provided on an "as-is" and "as-available" basis.
            </p>

            <h2>5. Governing Law</h2>
            <p>
              These Terms of Service are governed by and construed in accordance with the laws of the Federal Republic of Nigeria. Any disputes arising in connection with these terms shall be subject to the exclusive jurisdiction of Nigerian courts.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
