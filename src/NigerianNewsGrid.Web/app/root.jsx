import React, { useEffect } from "react";
import {
  isRouteErrorResponse,
  Links,
  Meta,
  Outlet,
  Scripts,
  ScrollRestoration,
  useLocation,
} from "react-router";
import "../src/styles/index.css";
import MobileBottomNav from "../src/components/MobileBottomNav";
import { GA_MEASUREMENT_ID, trackPageView } from "../src/services/analytics";

function AnalyticsTracker() {
  const location = useLocation();

  useEffect(() => {
    if (typeof window !== "undefined" && GA_MEASUREMENT_ID) {
      trackPageView(location.pathname + location.search, document.title);
    }
  }, [location.pathname, location.search]);

  return null;
}

export function Layout({ children }) {
  return (
    <html lang="en" data-theme="light" suppressHydrationWarning>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        {/* Apply persisted theme before first paint on every route (prevents light-on-light / flash) */}
        <script
          dangerouslySetInnerHTML={{
            __html: `(function(){try{var t=localStorage.getItem('nng_theme');document.documentElement.setAttribute('data-theme',t==='dark'?'dark':'light');}catch(e){}})();`,
          }}
        />
        <meta name="theme-color" content="#008751" />
        <link rel="manifest" href="/manifest.webmanifest" />
        <link rel="icon" type="image/png" href="/icon-192.png" />
        <link rel="apple-touch-icon" href="/icon-192.png" />
        <meta name="apple-mobile-web-app-capable" content="yes" />
        <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
        <meta name="apple-mobile-web-app-title" content="NewsStand NG" />
        <link
          rel="preload"
          href="/fonts/newsreader-latin.woff2"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
        <link
          rel="preload"
          href="/fonts/plus-jakarta-sans-latin.woff2"
          as="font"
          type="font/woff2"
          crossOrigin="anonymous"
        />
        <link
          rel="alternate"
          type="application/rss+xml"
          title="News Stand NG — Latest Headlines RSS Feed"
          href="/feed.xml"
        />
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{
            __html: JSON.stringify({
              "@context": "https://schema.org",
              "@type": "NewsMediaOrganization",
              "name": "News Stand NG",
              "alternateName": "Nigerian News Stand",
              "url": "https://newsstand.ng",
              "logo": {
                "@type": "ImageObject",
                "url": "https://newsstand.ng/logo.png",
                "width": 512,
                "height": 512
              },
              "sameAs": [
                "https://x.com/newsstandng",
                "https://facebook.com/newsstandng",
                "https://linkedin.com/company/newsstandng",
                "https://instagram.com/newsstandng"
              ],
              "publishingPrinciples": "https://newsstand.ng/editorial-policy",
              "correctionsPolicy": "https://newsstand.ng/corrections",
              "diversityPolicy": "https://newsstand.ng/editorial-policy#diversity",
              "ethicsPolicy": "https://newsstand.ng/editorial-policy#ethics",
              "knowsAbout": [
                "Nigerian Politics",
                "African Economy and Business",
                "Technology in Nigeria",
                "Security & Governance",
                "Sports & Culture"
              ],
              "contactPoint": [
                {
                  "@type": "ContactPoint",
                  "contactType": "editorial newsroom",
                  "email": "editorial@newsstand.ng",
                  "url": "https://newsstand.ng/contact"
                },
                {
                  "@type": "ContactPoint",
                  "contactType": "corrections desk",
                  "email": "corrections@newsstand.ng",
                  "url": "https://newsstand.ng/corrections"
                }
              ]
            })
          }}
        />
        <Meta />
        <Links />
        {GA_MEASUREMENT_ID ? (
          <>
            <script
              async
              src={`https://www.googletagmanager.com/gtag/js?id=${GA_MEASUREMENT_ID}`}
            />
            <script
              dangerouslySetInnerHTML={{
                __html: `
                  window.dataLayer = window.dataLayer || [];
                  function gtag(){dataLayer.push(arguments);}
                  gtag('js', new Date());
                  gtag('config', '${GA_MEASUREMENT_ID}', {
                    send_page_view: false
                  });
                `,
              }}
            />
          </>
        ) : null}
      </head>
      <body>
        {children}
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export default function Root() {
  return (
    <>
      <AnalyticsTracker />
      <Outlet />
      <MobileBottomNav />
    </>
  );
}

export function ErrorBoundary({ error }) {
  let message = "Oops!";
  let details = "An unexpected error occurred.";
  let stack;

  if (isRouteErrorResponse(error)) {
    message = error.status === 404 ? "404 — Story Not Found" : `Error ${error.status}`;
    details = error.statusText || (error.status === 404 ? "The requested article or topic does not exist or has been archived." : details);
  } else if (import.meta.env.DEV && error && error instanceof Error) {
    details = error.message;
    stack = error.stack;
  }

  return (
    <main style={{ padding: "5rem 2rem", maxWidth: "680px", margin: "0 auto", textAlign: "center", fontFamily: "var(--font-sans, system-ui, sans-serif)" }}>
      <div style={{ display: "inline-block", background: "rgba(0, 135, 81, 0.1)", color: "#008751", padding: "6px 14px", borderRadius: "999px", fontSize: "0.85rem", fontWeight: "700", marginBottom: "1.5rem" }}>
        NEWS STAND NG
      </div>
      <h1 style={{ fontSize: "2.2rem", fontWeight: "800", color: "#111827", marginBottom: "1rem" }}>{message}</h1>
      <p style={{ color: "#4B5563", fontSize: "1.1rem", lineHeight: "1.6", marginBottom: "2rem" }}>{details}</p>
      {stack && (
        <pre style={{ textAlign: "left", background: "#F3F4F6", color: "#DC2626", padding: "1rem", borderRadius: "8px", overflowX: "auto", fontSize: "0.85rem", marginBottom: "2rem" }}>
          <code>{stack}</code>
        </pre>
      )}
      <a
        href="/"
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: "8px",
          padding: "12px 24px",
          background: "#008751",
          color: "#ffffff",
          fontWeight: "600",
          textDecoration: "none",
          borderRadius: "8px",
          boxShadow: "0 4px 12px rgba(0, 135, 81, 0.2)"
        }}
      >
        Return to Live News Headlines
      </a>
    </main>
  );
}
