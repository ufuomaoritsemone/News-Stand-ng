export async function loader() {
  const baseUrl = 'https://newsstand.ng';
  const now = new Date().toISOString();

  const dataPages = [
    { path: '/naira-exchange-rate', priority: 0.9, freq: 'hourly' },
    { path: '/fuel-price-nigeria', priority: 0.8, freq: 'hourly' },
    { path: '/jamb-waec-news', priority: 0.8, freq: 'daily' },
    { path: '/elections', priority: 0.8, freq: 'daily' },
  ];

  let xml = '<?xml version="1.0" encoding="UTF-8"?>\n';
  xml += '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">\n';

  for (const page of dataPages) {
    xml += '  <url>\n';
    xml += `    <loc>${baseUrl}${page.path}</loc>\n`;
    xml += `    <lastmod>${now}</lastmod>\n`;
    xml += `    <changefreq>${page.freq}</changefreq>\n`;
    xml += `    <priority>${page.priority}</priority>\n`;
    xml += `    <xhtml:link rel="alternate" hreflang="en-ng" href="${baseUrl}${page.path}"/>\n`;
    xml += `    <xhtml:link rel="alternate" hreflang="pcm" href="${baseUrl}/pcm${page.path}"/>\n`;
    xml += `    <xhtml:link rel="alternate" hreflang="x-default" href="${baseUrl}${page.path}"/>\n`;
    xml += '  </url>\n';
  }

  xml += '</urlset>';

  return new Response(xml, {
    headers: {
      'Content-Type': 'application/xml; charset=utf-8',
      'Cache-Control': 'public, max-age=1800, s-maxage=1800',
    },
  });
}
