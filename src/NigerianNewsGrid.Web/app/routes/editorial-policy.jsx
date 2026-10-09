import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, CheckCircle2, ShieldCheck, Scale, FileText } from "lucide-react";

export function meta() {
  const title = "Editorial Policy & Verification Standards | News Stand NG";
  const description =
    "Read the editorial guidelines, verification principles, publisher neutrality standards, and opinion separation policies of News Stand NG.";
  const canonicalUrl = "https://newsstand.ng/editorial-policy";

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

export default function EditorialPolicyPage() {
  const policyJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "Editorial Policy — News Stand NG",
    "url": "https://newsstand.ng/editorial-policy",
    "description": "Editorial standards, verification practices, and non-partisanship guidelines for News Stand NG.",
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
        dangerouslySetInnerHTML={{ __html: JSON.stringify(policyJsonLd) }}
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
            <li className="breadcrumb-active" aria-current="page">Editorial Policy</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Editorial Policy & Standards</h1>
          <p className="static-policy-subtitle">
            Our framework for source verification, political neutrality, attribution transparency, and journalistic ethics across Nigeria.
          </p>

          <div className="static-policy-content">
            <h2>1. Source Selection & Whitelisting</h2>
            <p>
              News Stand NG does not aggregate from arbitrary or unverified blogs. To be included in our indexing pipeline, a news organization must meet strict foundational criteria:
            </p>
            <ul>
              <li><strong>Established Editorial Oversight:</strong> Must operate a recognized editorial board, masthead, or verified newsroom with bylined journalists.</li>
              <li><strong>Physical Presence & Transparency:</strong> Must maintain identifiable corporate registration and accessible newsroom contacts in Nigeria or global bureaus.</li>
              <li><strong>Record of Factual Journalism:</strong> Prior history free of systematic disinformation, hate speech, or defamatory fabrication.</li>
            </ul>

            <h2>2. Political Neutrality & Diversity of Voices</h2>
            <p>
              News Stand NG has no political affiliations, donor allegiances, or endorsements of any political candidate, party, or commercial conglomerate.
            </p>
            <p>
              Our clustering algorithms and featured sections are engineered to display diverse perspectives on critical national issues—balancing coverage from northern, southern, eastern, and western editorial viewpoints so citizens receive the complete picture.
            </p>

            <h2>3. Separation of News and Opinion</h2>
            <p>
              We enforce strict taxonomical separation between factual reporting and commentary:
            </p>
            <ul>
              <li>Articles categorized as <strong>Opinion</strong>, <strong>Editorial</strong>, or <strong>Column</strong> are explicitly marked in our system and excluded from hard news breaking feeds.</li>
              <li>Sponsored articles or native promotional content must carry visible <strong>Sponsored</strong> badges and <code>rel="sponsored noopener"</code> link relations, per Google Webmaster Guidelines and advertising ethics.</li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Diversity & Geographic Inclusivity:</strong> Nigeria's strength lies in its 250+ ethnic groups and diverse cultural heritage. Our editorial ingestion monitors local news across all six geopolitical zones: North-Central, North-East, North-West, South-East, South-South, and South-West.
            </div>

            <h2>4. Handling Corrections & Errors</h2>
            <p>
              Because we aggregate live stories, when an originating publisher updates or retracts a story, our ingestion pipeline refreshes the headline, timestamp, and excerpt during recurring synchronization passes. Readers and publishers can also notify us directly of disputed information via our <Link to="/corrections">Corrections Desk</Link>.
            </p>

            <h2>5. Respect for Copyright & Intellectual Property</h2>
            <p>
              We believe robust investigative journalism requires economic sustainability. News Stand NG never paywalls aggregated snippets, never hides origin attribution, and never replaces publisher destination links with ad-interstitials.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
