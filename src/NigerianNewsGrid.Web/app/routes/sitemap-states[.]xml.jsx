import { api } from '../../src/services/api';

const ALL_STATE_SLUGS = [
  'abia', 'adamawa', 'akwa-ibom', 'anambra', 'bauchi', 'bayelsa', 'benue', 'borno',
  'cross-river', 'delta', 'ebonyi', 'edo', 'ekiti', 'enugu', 'fct', 'gombe',
  'imo', 'jigawa', 'kaduna', 'kano', 'katsina', 'kebbi', 'kogi', 'kwara',
  'lagos', 'nasarawa', 'niger', 'ogun', 'ondo', 'osun', 'oyo', 'plateau',
  'rivers', 'sokoto', 'taraba', 'yobe', 'zamfara'
];

export async function loader() {
  const baseUrl = 'https://newsstand.ng';
  const now = new Date().toISOString();

  let xml = '<?xml version="1.0" encoding="UTF-8"?>\n';
  xml += '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">\n';

  // States directory index
  xml += '  <url>\n';
  xml += `    <loc>${baseUrl}/states</loc>\n`;
  xml += `    <lastmod>${now}</lastmod>\n`;
  xml += '    <changefreq>daily</changefreq>\n';
  xml += '    <priority>0.8</priority>\n';
  xml += `    <xhtml:link rel="alternate" hreflang="en-ng" href="${baseUrl}/states"/>\n`;
  xml += `    <xhtml:link rel="alternate" hreflang="pcm" href="${baseUrl}/pcm/states"/>\n`;
  xml += `    <xhtml:link rel="alternate" hreflang="x-default" href="${baseUrl}/states"/>\n`;
  xml += '  </url>\n';

  for (const slug of ALL_STATE_SLUGS) {
    xml += '  <url>\n';
    xml += `    <loc>${baseUrl}/state/${slug}</loc>\n`;
    xml += `    <lastmod>${now}</lastmod>\n`;
    xml += '    <changefreq>daily</changefreq>\n';
    xml += '    <priority>0.7</priority>\n';
    xml += `    <xhtml:link rel="alternate" hreflang="en-ng" href="${baseUrl}/state/${slug}"/>\n`;
    xml += `    <xhtml:link rel="alternate" hreflang="pcm" href="${baseUrl}/pcm/state/${slug}"/>\n`;
    xml += `    <xhtml:link rel="alternate" hreflang="x-default" href="${baseUrl}/state/${slug}"/>\n`;
    xml += '  </url>\n';
  }

  xml += '</urlset>';

  return new Response(xml, {
    headers: {
      'Content-Type': 'application/xml; charset=utf-8',
      'Cache-Control': 'public, max-age=3600, s-maxage=3600',
    },
  });
}
