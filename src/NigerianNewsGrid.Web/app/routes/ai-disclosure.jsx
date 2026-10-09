import React from "react";
import { Link } from "react-router";
import Footer from "../../src/components/Footer";
import { ArrowLeft, AlertCircle, Sparkles, Brain, Cpu, ShieldCheck, CheckCircle2 } from "lucide-react";

export function meta() {
  const title = "AI & Machine Learning Disclosure | News Stand NG";
  const description =
    "Read how News Stand NG utilizes artificial intelligence and machine learning models for topic clustering, audio synthesis, and concise editorial summarization.";
  const canonicalUrl = "https://newsstand.ng/ai-disclosure";

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

export default function AiDisclosurePage() {
  const aiJsonLd = {
    "@context": "https://schema.org",
    "@type": "WebPage",
    "name": "AI & Machine Learning Disclosure — News Stand NG",
    "url": "https://newsstand.ng/ai-disclosure",
    "description": "Policy and transparency statement on the use of machine learning, entity extraction, and AI summarization at News Stand NG.",
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
        dangerouslySetInnerHTML={{ __html: JSON.stringify(aiJsonLd) }}
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
            <li className="breadcrumb-active" aria-current="page">AI Disclosure</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="draft-review-badge">
            <AlertCircle size={14} />
            <span>DRAFT – review before launch</span>
          </div>

          <h1 className="static-policy-title">Artificial Intelligence & Automation Disclosure</h1>
          <p className="static-policy-subtitle">
            Google E-E-A-T and transparency pledge: How News Stand NG leverages machine learning to index, categorize, and summarize Nigerian news responsibly.
          </p>

          <div className="static-policy-content">
            <h2>1. Why and How We Use AI</h2>
            <p>
              In modern digital news operations, thousands of articles are published daily across Nigerian news outlets. To make this immense volume of information navigable, News Stand NG employs specialized artificial intelligence and machine learning models for specific, bounded tasks:
            </p>
            <ul>
              <li><strong>Topic Clustering & Entity Extraction:</strong> Detecting named entities (such as public officials, government agencies, geographical regions, and economic themes) to automatically group related reports across multiple publishers.</li>
              <li><strong>Editorial Excerpt & Key Takeaways:</strong> Generating concise 2–3 bullet point summaries strictly grounded in verified excerpts to help readers evaluate whether to read the full report.</li>
              <li><strong>Text-to-Speech (TTS) Audio Briefings:</strong> Converting textual summaries into clear audio newscasts for hands-free listening on mobile devices.</li>
              <li><strong>Categorization & De-duplication:</strong> Identifying syndicated wire stories published simultaneously across multiple outlets to reduce repetitive feed clutter.</li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Strict Factual Grounding:</strong> News Stand NG forbids generative AI from creating synthetic "rewrites", hallucinating unverified quotes, fabricating statistics, or authoring original reporting without human origin attribution. Every AI-assisted summary is strictly constrained to the facts presented by the original reporting publisher.
            </div>

            <h2>2. Human Oversight & Editorial Review</h2>
            <p>
              Automated pipelines are overseen by human editors:
            </p>
            <ul>
              <li><strong>Validation Rules:</strong> Automated filters enforce strict profanity, defamation, length, and neutrality validators before any algorithmic summary or metadata is published.</li>
              <li><strong>Human In The Loop:</strong> Topic introductions, investigative hub dossiers, and trending cluster overviews undergo editorial newsroom review.</li>
              <li><strong>Correction Channel:</strong> If an automated summary ever distorts or mischaracterizes a story's nuance, readers and publishers can trigger immediate human review via <a href="mailto:corrections@newsstand.ng">corrections@newsstand.ng</a>.</li>
            </ul>

            <h2>3. Respecting Original Journalists</h2>
            <p>
              We believe AI must enhance human journalism, never cannibalize it. Our AI models do not ingest paywalled content, do not strip author bylines, and always direct readers to the original news organization for full reporting.
            </p>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}
