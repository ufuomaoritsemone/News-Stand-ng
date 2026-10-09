import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, Shield, Lock, Eye, CheckCircle2 } from "lucide-react";

export function meta() {
  const title = "Privacy Policy & Data Protection | News Stand NG";
  const description =
    "Understand how News Stand NG handles data, local storage, analytics, and user privacy in full compliance with the Nigeria Data Protection Act (NDPA).";
  const canonicalUrl = "https://newsstand.ng/privacy";

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

export default function PrivacyPage() {
  const privacyJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "Privacy Policy — News Stand NG",
    "url": "https://newsstand.ng/privacy",
    "description": "Privacy policy and NDPA compliance statement for News Stand NG.",
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
        dangerouslySetInnerHTML={{ __html: JSON.stringify(privacyJsonLd) }}
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
            <li className="breadcrumb-active" aria-current="page">Privacy Policy</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Privacy Policy & NDPA Compliance</h1>
          <p className="static-policy-subtitle">
            Effective Date: October 2026. How we protect your personal data under the Nigeria Data Protection Act (NDPA) and global privacy benchmarks.
          </p>

          <div className="static-policy-content">
            <h2>1. Information We Collect</h2>
            <p>
              News Stand NG is built with a <strong>privacy-first architecture</strong>. You can read news, explore topic hubs, and search national headlines completely anonymously without creating an account or providing personal identification.
            </p>
            <p>
              We collect minimal data strictly required to deliver service functionality:
            </p>
            <ul>
              <li><strong>Anonymous Usage & Telemetry:</strong> Aggregated page view counts, device type (mobile/desktop), and general geographic origin (e.g. state or city level) to optimize content delivery networks across Nigeria.</li>
              <li><strong>Local Storage Data:</strong> Bookmarked articles, dark/light theme preferences, and recently viewed stories are stored locally on your device (via <code>localStorage</code>) and are never uploaded to our servers unless you authenticate.</li>
              <li><strong>Optional Account Data:</strong> If you sign in via Google OAuth or register an account, we receive your email address, display name, and avatar URL to synchronize your bookmarks and comment history.</li>
            </ul>

            <h2>2. Cookies & Tracking Technologies</h2>
            <p>
              We do not use invasive third-party cross-site tracking cookies. We utilize essential session tokens and performance caching headers to optimize mobile page load speeds and prevent denial-of-service traffic.
            </p>

            <h2>3. Nigeria Data Protection Act (NDPA) Rights</h2>
            <p>
              Under the Nigeria Data Protection Act (NDPA) 2023, Nigerian residents possess enforceable rights concerning their personal data:
            </p>
            <ul>
              <li><strong>Right of Access:</strong> Request a copy of the personal information stored in your account.</li>
              <li><strong>Right to Rectification:</strong> Update inaccurate account details or comment history.</li>
              <li><strong>Right to Erasure ("Right to be Forgotten"):</strong> Request immediate and permanent deletion of your account, bookmarks, and submitted comments.</li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Data Protection Officer (DPO):</strong> To exercise any data subject rights or inquire about our data retention practices, email our Data Protection Officer at <a href="mailto:privacy@newsstand.ng">privacy@newsstand.ng</a>.
            </div>

            <h2>4. Third-Party Publisher Links</h2>
            <p>
              When you click an outbound link (e.g., <code>Read full story on Punch</code>), you transition to an external third-party website operated by that independent news organization. We encourage you to review the individual privacy policies of external media websites you visit.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
