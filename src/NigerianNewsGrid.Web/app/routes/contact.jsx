import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, Mail, MapPin, Phone, Building, Send } from "lucide-react";

export function meta() {
  const title = "Contact News Stand NG — Editorial Desks, Partnerships & Support";
  const description =
    "Contact News Stand NG editorial desks, developer support, corrections department, or commercial partnership teams in Lagos and Abuja.";
  const canonicalUrl = "https://newsstand.ng/contact";

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

export default function ContactPage() {
  const contactJsonLd = {
    "@context": "https://schema.org",
    "@type": "ContactPage",
    "name": "Contact News Stand NG",
    "url": "https://newsstand.ng/contact",
    "description": "Contact information for News Stand NG newsroom, corrections, partnerships, and headquarters.",
    "mainEntity": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng",
      "contactPoint": [
        {
          "@type": "ContactPoint",
          "contactType": "editorial newsroom",
          "email": "editorial@newsstand.ng"
        },
        {
          "@type": "ContactPoint",
          "contactType": "corrections desk",
          "email": "corrections@newsstand.ng"
        },
        {
          "@type": "ContactPoint",
          "contactType": "commercial partnerships",
          "email": "partnerships@newsstand.ng"
        }
      ]
    }
  };

  return (
    <div className="static-page-layout">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(contactJsonLd) }}
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
            <li className="breadcrumb-active" aria-current="page">Contact Us</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Contact News Stand NG</h1>
          <p className="static-policy-subtitle">
            Get in touch with our editorial desks, publisher relations team, engineering support, or legal representatives.
          </p>

          <div className="static-policy-content">
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "20px", margin: "24px 0 36px 0" }}>
              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "var(--brand-green, #10b981)", marginBottom: "8px", fontWeight: 700 }}>
                  <Mail size={18} />
                  <span>Editorial & Newsroom</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.9rem" }}>Tips, press releases, and editorial feedback:</p>
                <a href="mailto:editorial@newsstand.ng" style={{ fontWeight: 600 }}>editorial@newsstand.ng</a>
              </div>

              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#f59e0b", marginBottom: "8px", fontWeight: 700 }}>
                  <AlertCircle size={18} />
                  <span>Corrections & Retractions</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.9rem" }}>Report factual inaccuracies or broken links:</p>
                <a href="mailto:corrections@newsstand.ng" style={{ fontWeight: 600 }}>corrections@newsstand.ng</a>
              </div>

              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#38bdf8", marginBottom: "8px", fontWeight: 700 }}>
                  <Building size={18} />
                  <span>Publishers & Syndication</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.9rem" }}>Feed integration, RSS whitelisting, or API access:</p>
                <a href="mailto:publishers@newsstand.ng" style={{ fontWeight: 600 }}>publishers@newsstand.ng</a>
              </div>

              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#ec4899", marginBottom: "8px", fontWeight: 700 }}>
                  <MapPin size={18} />
                  <span>Headquarters & Bureaus</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.9rem" }}>Lagos: Victoria Island, Lagos State, Nigeria</p>
                <p style={{ margin: 0, fontSize: "0.9rem" }}>Abuja: Central Business District, FCT, Nigeria</p>
              </div>
            </div>

            <h2>Social Handles & Verified Profiles</h2>
            <p>
              Connect with our live alert streams on social platforms:
            </p>
            <ul>
              <li><strong>X (Twitter):</strong> <a href="https://x.com/newsstandng" target="_blank" rel="noopener noreferrer">@newsstandng</a></li>
              <li><strong>Facebook:</strong> <a href="https://facebook.com/newsstandng" target="_blank" rel="noopener noreferrer">facebook.com/newsstandng</a></li>
              <li><strong>LinkedIn:</strong> <a href="https://linkedin.com/company/newsstandng" target="_blank" rel="noopener noreferrer">linkedin.com/company/newsstandng</a></li>
              <li><strong>Instagram:</strong> <a href="https://instagram.com/newsstandng" target="_blank" rel="noopener noreferrer">@newsstandng</a></li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Emergency DMCA or Copyright Inquiries:</strong> If you are a copyright owner wishing to adjust excerpting parameters or submit takedown requests, contact our legal counsel directly at <a href="mailto:legal@newsstand.ng">legal@newsstand.ng</a>.
            </div>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
