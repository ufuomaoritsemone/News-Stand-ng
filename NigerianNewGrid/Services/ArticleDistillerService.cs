using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace NigerianNewGrid.Services;

public sealed record DistilledArticleResult
{
    public string Title { get; init; } = string.Empty;
    public string PublisherName { get; init; } = string.Empty;
    public string? PublisherLogoUrl { get; init; }
    public string PublisherDomain { get; init; } = string.Empty;
    public string BrandColor { get; init; } = "#1B3B6F";
}

public class ArticleDistillerService
{
    public record PublisherInfo(string Name, string? LogoUrl, string BrandColor, string AccentColor);

    /// <summary>
    /// Identifies publisher branding and logos for Nigerian and international news outlets.
    /// </summary>
    public static PublisherInfo IdentifyPublisher(string url)
    {
        var u = url?.ToLowerInvariant() ?? string.Empty;

        if (u.Contains("punchng.com") || u.Contains("punchng"))
            return new PublisherInfo("The Punch", "https://punchng.com/wp-content/themes/punchng/assets/images/logo.png", "#0B6623", "#004D1A");

        if (u.Contains("vanguardngr.com") || u.Contains("vanguard"))
            return new PublisherInfo("Vanguard News", "https://www.vanguardngr.com/wp-content/uploads/2020/09/logo-main.png", "#C41230", "#8A0B20");

        if (u.Contains("premiumtimesng.com") || u.Contains("premiumtimes"))
            return new PublisherInfo("Premium Times", "https://www.premiumtimesng.com/wp-content/themes/premiumtimes/assets/img/logo.png", "#E31B23", "#990000");

        if (u.Contains("thecable.ng") || u.Contains("thecable"))
            return new PublisherInfo("TheCable", "https://www.thecable.ng/wp-content/themes/thecable/assets/images/logo.png", "#FF5722", "#D84315");

        if (u.Contains("dailytrust.com") || u.Contains("dailytrust"))
            return new PublisherInfo("Daily Trust", "https://dailytrust.com/wp-content/themes/dailytrust/assets/img/logo.png", "#0056B3", "#003366");

        if (u.Contains("guardian.ng") || u.Contains("guardian"))
            return new PublisherInfo("The Guardian Nigeria", "https://guardian.ng/wp-content/themes/guardian-ng/assets/images/logo.png", "#222222", "#000000");

        if (u.Contains("channelstv.com") || u.Contains("channelstv"))
            return new PublisherInfo("Channels Television", "https://www.channelstv.com/wp-content/uploads/2018/06/channels-logo.png", "#003399", "#001F5C");

        if (u.Contains("saharareporters.com") || u.Contains("saharareporters"))
            return new PublisherInfo("Sahara Reporters", "https://saharareporters.com/sites/default/files/logo_0.png", "#D32F2F", "#B71C1C");

        if (u.Contains("businessday.ng") || u.Contains("businessday"))
            return new PublisherInfo("BusinessDay", "https://businessday.ng/wp-content/uploads/2021/04/bday-logo-new.png", "#B22222", "#800000");

        if (u.Contains("tribuneonlineng.com") || u.Contains("tribune"))
            return new PublisherInfo("Nigerian Tribune", "https://tribuneonlineng.com/wp-content/uploads/2019/08/tribune-logo.png", "#C2185B", "#880E4F");

        if (u.Contains("leadership.ng") || u.Contains("leadership"))
            return new PublisherInfo("Leadership", "https://leadership.ng/wp-content/uploads/2021/08/leadership-logo.png", "#D32F2F", "#333333");

        if (u.Contains("arise.tv") || u.Contains("arise"))
            return new PublisherInfo("Arise News", "https://www.arise.tv/wp-content/uploads/2020/12/arise-logo-new.png", "#E50914", "#B81D24");

        if (u.Contains("bbc.com") || u.Contains("bbc"))
            return new PublisherInfo("BBC News", "https://static.files.bbci.co.uk/ws/simorgh-assets/public/pidgin/images/metadata/poster-1024x576.png", "#BB1919", "#800C0C");

        if (u.Contains("lindaikejisblog.com") || u.Contains("lindaikeji"))
            return new PublisherInfo("Linda Ikeji's Blog", "https://www.lindaikejisblog.com/favicon.ico", "#E91E63", "#C2185B");

        var domain = GetDomainName(url ?? "");
        var capitalized = FormatDomainForDisplay(domain);
        return new PublisherInfo(capitalized, null, "#1B3B6F", "#0A192F");
    }

    private static string GetDomainName(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();
                return host.StartsWith("www.") ? host[4..] : host;
            }
        }
        catch { }
        return "news";
    }

    private static string FormatDomainForDisplay(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return "News Publisher";
        var parts = domain.Split('.');
        if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
        {
            var name = parts[0];
            return char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name[1..] : "");
        }
        return domain;
    }

    /// <summary>
    /// Calculates estimated reading time in minutes based on average reading speed (200 WPM).
    /// </summary>
    public static (int Minutes, string Label) CalculateReadingTime(string? text, int wordsPerMinute = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (2, "2 min read");
        }

        var words = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var minutes = Math.Max(1, (int)Math.Ceiling((double)words.Length / Math.Max(1, wordsPerMinute)));
        return (minutes, $"{minutes} min read");
    }

    /// <summary>
    /// Generates the Chromium / Chrome Reader Mode DOM Distiller script.
    /// Runs directly inside the live rendered WebView DOM to harvest ALL body text,
    /// headlines, and images while stripping all advertisements and bloat.
    /// </summary>
    public static string GenerateChromeReaderDistillerScript(
        string publisherName,
        string? publisherLogoUrl,
        string brandColor,
        string? fallbackTitle,
        string? fallbackImageUrl,
        string? category,
        bool active = true,
        string fontSize = "18px")
    {
        var safePubName = HttpUtility.JavaScriptStringEncode(publisherName ?? "News");
        var safeLogoUrl = HttpUtility.JavaScriptStringEncode(publisherLogoUrl ?? "");
        var safeBrandColor = HttpUtility.JavaScriptStringEncode(brandColor ?? "#1B3B6F");
        var safeTitle = HttpUtility.JavaScriptStringEncode(fallbackTitle ?? "");
        var safeImg = HttpUtility.JavaScriptStringEncode(fallbackImageUrl ?? "");
        var safeCat = HttpUtility.JavaScriptStringEncode(category ?? "News");
        var activeStr = active ? "true" : "false";
        var safeFontSize = HttpUtility.JavaScriptStringEncode(fontSize ?? "18px");

        var sb = new StringBuilder();
        sb.AppendLine("(function() {");
        sb.AppendLine("    try {");
        sb.AppendLine("        window.__nngPublisher = {");
        sb.AppendLine($"            name: '{safePubName}',");
        sb.AppendLine($"            logo: '{safeLogoUrl}',");
        sb.AppendLine($"            brandColor: '{safeBrandColor}',");
        sb.AppendLine($"            fallbackTitle: '{safeTitle}',");
        sb.AppendLine($"            fallbackImage: '{safeImg}',");
        sb.AppendLine($"            category: '{safeCat}',");
        sb.AppendLine($"            initialActive: {activeStr},");
        sb.AppendLine($"            fontSize: '{safeFontSize}'");
        sb.AppendLine("        };");

        sb.AppendLine(@"
        if (!window.__nngReaderEngine) {
            window.__nngReaderEngine = {
                isActive: false,
                readerContainer: null,

                init: function() {
                    this.distillAndMount();
                },

                distillAndMount: function() {
                    const cfg = window.__nngPublisher;

                    // 1. Identify Headline
                    let title = cfg.fallbackTitle || '';
                    const ogTitle = document.querySelector('meta[property=""og:title""], meta[name=""twitter:title""]');
                    const h1 = document.querySelector('h1');
                    if (ogTitle && ogTitle.content && ogTitle.content.trim().length > 5) {
                        title = ogTitle.content.trim();
                    } else if (h1 && h1.innerText && h1.innerText.trim().length > 5) {
                        title = h1.innerText.trim();
                    } else if (!title) {
                        title = document.title ? document.title.split(' - ')[0].split(' | ')[0].trim() : 'News Article';
                    }

                    // 2. Identify Head Picture / Lead Image
                    let leadImage = cfg.fallbackImage || '';
                    const ogImg = document.querySelector('meta[property=""og:image""], meta[name=""twitter:image""]');
                    if (ogImg && ogImg.content && ogImg.content.startsWith('http')) {
                        leadImage = ogImg.content;
                    } else if (!leadImage) {
                        const firstArticleImg = document.querySelector('article img, .post-content img, .entry-content img, figure img');
                        if (firstArticleImg && firstArticleImg.src && firstArticleImg.src.startsWith('http') && !firstArticleImg.src.includes('logo') && !firstArticleImg.src.includes('avatar')) {
                            leadImage = firstArticleImg.src;
                        }
                    }

                    // 3. Identify Author Byline & Date
                    let author = '';
                    const authorMeta = document.querySelector('meta[name=""author""], meta[property=""article:author""]');
                    if (authorMeta && authorMeta.content) {
                        author = authorMeta.content.trim();
                    } else {
                        const bylineEl = document.querySelector('.byline, .author, .post-author, [rel=""author""], .article-author');
                        if (bylineEl) author = bylineEl.innerText.trim();
                    }

                    let dateStr = '';
                    const timeEl = document.querySelector('time, [itemprop=""datePublished""], meta[property=""article:published_time""]');
                    if (timeEl) {
                        dateStr = timeEl.innerText || timeEl.getAttribute('content') || timeEl.getAttribute('datetime') || '';
                    }
                    if (!dateStr) {
                        dateStr = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                    }

                    // 4. Algorithm: Google Chrome DOM Distiller / Readability Paragraph Harvester
                    const containerSelectors = [
                        'article',
                        '[itemprop=""articleBody""]',
                        '.entry-content',
                        '.post-content',
                        '.article-content',
                        '.story-body',
                        '.article__body',
                        '.article_body',
                        '.story__content',
                        '.content-inner',
                        '.story-content',
                        '.main-content',
                        'main'
                    ];

                    let bestContainer = null;
                    let maxScore = 0;

                    for (const sel of containerSelectors) {
                        const matches = document.querySelectorAll(sel);
                        matches.forEach(el => {
                            const paras = el.querySelectorAll('p');
                            let textLen = 0;
                            paras.forEach(p => { textLen += p.innerText.trim().length; });
                            const score = paras.length * 60 + textLen;
                            if (score > maxScore) {
                                maxScore = score;
                                bestContainer = el;
                            }
                        });
                    }

                    if (!bestContainer || maxScore < 200) {
                        document.querySelectorAll('div, section, main, article').forEach(el => {
                            const paras = el.querySelectorAll(':scope > p, :scope > div > p');
                            let textLen = 0;
                            paras.forEach(p => { textLen += p.innerText.trim().length; });
                            const score = paras.length * 60 + textLen;
                            if (score > maxScore) {
                                maxScore = score;
                                bestContainer = el;
                            }
                        });
                    }

                    const sourceContainer = bestContainer || document.body;
                    const rawBlocks = sourceContainer.querySelectorAll('p, h2, h3, h4, blockquote, figure');
                    const contentHtmlList = [];
                    const seenTexts = new Set();

                    rawBlocks.forEach(node => {
                        const tag = node.tagName.toLowerCase();
                        const text = node.innerText ? node.innerText.trim() : '';
                        const lower = text.toLowerCase();

                        if (node.closest('.ad, .advert, .advertisement, .adsbygoogle, .google-auto-placed, .taboola, .outbrain, .newsletter, .sidebar, .comments, footer, header, nav, .share-buttons, .social-share, [id*=""google_ads""], .cookie-banner, .ezoic, .mgid')) return;
                        if (lower.includes('read also:') || lower.includes('see also:') || lower.includes('click here to') || lower.includes('join our whatsapp') || lower.includes('all rights reserved') || lower.includes('download our app')) return;

                        if (seenTexts.has(text)) return;
                        if (text.length > 0) seenTexts.add(text);

                        if (tag === 'p') {
                            if (text.length > 15) {
                                contentHtmlList.push('<p class=""nng-reader-p"">' + node.innerHTML + '</p>');
                            }
                        } else if (tag === 'h2' || tag === 'h3' || tag === 'h4') {
                            if (text.length > 3 && text.length < 150) {
                                contentHtmlList.push('<h3 class=""nng-reader-h3"">' + node.innerHTML + '</h3>');
                            }
                        } else if (tag === 'blockquote') {
                            if (text.length > 10) {
                                contentHtmlList.push('<blockquote class=""nng-reader-quote"">' + node.innerHTML + '</blockquote>');
                            }
                        } else if (tag === 'figure') {
                            const img = node.querySelector('img');
                            const caption = node.querySelector('figcaption');
                            if (img && img.src && img.src.startsWith('http') && img.src !== leadImage && !img.src.includes('logo') && !img.src.includes('icon')) {
                                const captionHtml = caption ? '<figcaption class=""nng-reader-caption"">' + caption.innerText.trim() + '</figcaption>' : '';
                                contentHtmlList.push('<figure class=""nng-reader-fig""><img src=""' + img.src + '"" class=""nng-reader-inline-img"" loading=""lazy"" />' + captionHtml + '</figure>');
                            }
                        }
                    });

                    if (contentHtmlList.length < 2) {
                        document.querySelectorAll('p').forEach(p => {
                            const t = p.innerText ? p.innerText.trim() : '';
                            if (t.length > 25 && !t.toLowerCase().includes('copyright') && !t.toLowerCase().includes('rights reserved') && !p.closest('header, footer, nav, aside')) {
                                if (!seenTexts.has(t)) {
                                    seenTexts.add(t);
                                    contentHtmlList.push('<p class=""nng-reader-p"">' + p.innerHTML + '</p>');
                                }
                            }
                        });
                    }

                    const totalWords = contentHtmlList.join(' ').replace(/<[^>]*>/g, ' ').split(/\s+/).filter(Boolean).length;
                    const readingTime = Math.max(1, Math.ceil(totalWords / 200));

                    // 5. Build Google Chrome Reader Mode DOM Element
                    let readerEl = document.getElementById('nng-chrome-reader-container');
                    if (!readerEl) {
                        readerEl = document.createElement('div');
                        readerEl.id = 'nng-chrome-reader-container';
                        document.documentElement.appendChild(readerEl);
                    }

                    const logoHtml = cfg.logo 
                        ? '<img src=""' + cfg.logo + '"" alt=""' + cfg.name + '"" class=""nng-publisher-logo"" onerror=""this.style.display=\'none\'; document.getElementById(\'nng-pub-text\').style.display=\'block\';"" /><span id=""nng-pub-text"" class=""nng-publisher-text"" style=""display:none;"">' + cfg.name + '</span>'
                        : '<span class=""nng-publisher-text"">' + cfg.name + '</span>';

                    const leadImageHtml = leadImage
                        ? '<div class=""nng-lead-image-wrap""><img src=""' + leadImage + '"" alt=""' + title + '"" class=""nng-lead-image"" /></div>'
                        : '';

                    const authorHtml = author ? '<span>By <strong>' + author + '</strong></span> • ' : '';

                    readerEl.innerHTML = `
                        <div class=""nng-reader-inner"">
                            <div class=""nng-reader-masthead"">
                                <div class=""nng-masthead-left"">
                                    ${logoHtml}
                                </div>
                                <div class=""nng-masthead-badge"">
                                    ⚡ Clean Reader
                                </div>
                            </div>

                            <div class=""nng-category-tag"">${cfg.category || 'News'}</div>
                            <h1 class=""nng-article-title"">${title}</h1>

                            <div class=""nng-article-meta"">
                                ${authorHtml}
                                <span>${dateStr}</span> • 
                                <span>${readingTime} min read</span>
                            </div>

                            ${leadImageHtml}

                            <div class=""nng-article-body"">
                                ${contentHtmlList.join('')}
                            </div>

                            <div class=""nng-reader-footer"">
                                ✨ Clean Reading View • News Stand NG
                            </div>
                        </div>
                    `;

                    this.readerContainer = readerEl;
                    this.injectStyles(cfg.brandColor, cfg.fontSize);

                    if (cfg.initialActive) {
                        this.setReaderActive(true);
                    }
                },

                injectStyles: function(brandColor, fontSize) {
                    let styleEl = document.getElementById('nng-chrome-reader-style');
                    if (!styleEl) {
                        styleEl = document.createElement('style');
                        styleEl.id = 'nng-chrome-reader-style';
                        document.head.appendChild(styleEl);
                    }

                    styleEl.innerHTML = `
                        :root {
                            --nng-font-size: ` + (fontSize || '18px') + `;
                            --nng-accent: ` + (brandColor || '#1B3B6F') + `;
                        }

                        #nng-chrome-reader-container {
                            position: fixed !important;
                            top: 0 !important;
                            left: 0 !important;
                            right: 0 !important;
                            bottom: 0 !important;
                            width: 100vw !important;
                            height: 100vh !important;
                            background: #FFFFFF !important;
                            color: #1A202C !important;
                            z-index: 2147483647 !important;
                            overflow-y: auto !important;
                            -webkit-overflow-scrolling: touch !important;
                            box-sizing: border-box !important;
                            display: none;
                        }

                        @media (prefers-color-scheme: dark) {
                            #nng-chrome-reader-container {
                                background: #0F172A !important;
                                color: #F1F5F9 !important;
                            }
                        }

                        .nng-reader-inner {
                            max-width: 680px !important;
                            margin: 0 auto !important;
                            padding: 24px 18px 100px 18px !important;
                            box-sizing: border-box !important;
                        }

                        .nng-reader-masthead {
                            display: flex !important;
                            align-items: center !important;
                            justify-content: space-between !important;
                            padding-bottom: 12px !important;
                            margin-bottom: 18px !important;
                            border-bottom: 2px solid rgba(128, 128, 128, 0.2) !important;
                        }

                        .nng-publisher-logo {
                            max-height: 38px !important;
                            max-width: 180px !important;
                            object-fit: contain !important;
                            display: block !important;
                        }

                        .nng-publisher-text {
                            font-family: 'LegacySerifBold', 'LegacySerifITCTTBold', 'Georgia', serif !important;
                            font-size: 20px !important;
                            font-weight: 800 !important;
                            color: var(--nng-accent) !important;
                            text-transform: uppercase !important;
                        }

                        .nng-masthead-badge {
                            font-family: 'LegacySansBook', -apple-system, BlinkMacSystemFont, sans-serif !important;
                            font-size: 11px !important;
                            font-weight: 700 !important;
                            background: var(--nng-accent) !important;
                            color: #FFFFFF !important;
                            padding: 4px 10px !important;
                            border-radius: 12px !important;
                            text-transform: uppercase !important;
                            letter-spacing: 0.5px !important;
                        }

                        .nng-category-tag {
                            font-family: 'LegacySansBook', -apple-system, BlinkMacSystemFont, sans-serif !important;
                            font-size: 12px !important;
                            font-weight: 800 !important;
                            color: var(--nng-accent) !important;
                            text-transform: uppercase !important;
                            letter-spacing: 0.8px !important;
                            margin-bottom: 8px !important;
                        }

                        .nng-article-title {
                            font-family: 'LegacySerifITCTTBold', 'LegacySerifBold', 'Georgia', 'Cambria', serif !important;
                            font-size: 28px !important;
                            line-height: 1.28 !important;
                            font-weight: 800 !important;
                            margin: 0 0 14px 0 !important;
                            color: inherit !important;
                            letter-spacing: -0.3px !important;
                        }

                        .nng-article-meta {
                            font-family: 'Legacy Sans Book', 'LegacySansBook', -apple-system, BlinkMacSystemFont, sans-serif !important;
                            font-size: 13px !important;
                            color: #718096 !important;
                            margin-bottom: 20px !important;
                            line-height: 1.5 !important;
                        }

                        .nng-lead-image-wrap {
                            margin: 0 -18px 24px -18px !important;
                            border-radius: 0 !important;
                            overflow: hidden !important;
                            background: rgba(128,128,128,0.1) !important;
                        }

                        .nng-lead-image {
                            width: 100% !important;
                            height: auto !important;
                            max-height: 420px !important;
                            object-fit: cover !important;
                            display: block !important;
                        }

                        .nng-article-body {
                            font-family: 'Legacy Sans Book', 'LegacySansBook', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif !important;
                            font-size: var(--nng-font-size) !important;
                            line-height: 1.78 !important;
                            color: inherit !important;
                        }

                        .nng-reader-p {
                            font-family: 'Legacy Sans Book', 'LegacySansBook', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif !important;
                            font-size: var(--nng-font-size) !important;
                            line-height: 1.78 !important;
                            margin: 0 0 22px 0 !important;
                            color: inherit !important;
                            word-break: break-word !important;
                        }

                        .nng-reader-h3 {
                            font-family: 'LegacySerifBold', 'LegacySerifITCTTBold', 'Georgia', serif !important;
                            font-size: calc(var(--nng-font-size) * 1.22) !important;
                            font-weight: 700 !important;
                            margin: 32px 0 14px 0 !important;
                            line-height: 1.35 !important;
                            color: inherit !important;
                        }

                        .nng-reader-quote {
                            margin: 24px 0 !important;
                            padding: 14px 18px !important;
                            border-left: 4px solid var(--nng-accent) !important;
                            background: rgba(128,128,128,0.07) !important;
                            border-radius: 0 8px 8px 0 !important;
                            font-family: 'LegacySerifBold', 'Georgia', serif !important;
                            font-style: italic !important;
                            font-size: calc(var(--nng-font-size) * 1.08) !important;
                            line-height: 1.6 !important;
                        }

                        .nng-reader-fig {
                            margin: 24px 0 !important;
                            text-align: center !important;
                        }

                        .nng-reader-inline-img {
                            max-width: 100% !important;
                            height: auto !important;
                            border-radius: 10px !important;
                        }

                        .nng-reader-caption {
                            font-size: 12px !important;
                            color: #718096 !important;
                            margin-top: 6px !important;
                        }

                        .nng-reader-footer {
                            margin-top: 40px !important;
                            padding-top: 20px !important;
                            border-top: 1px solid rgba(128,128,128,0.2) !important;
                            text-align: center !important;
                            font-size: 13px !important;
                            color: #718096 !important;
                        }
                    `;
                },

                setReaderActive: function(active) {
                    this.isActive = active;
                    const container = document.getElementById('nng-chrome-reader-container');
                    if (container) {
                        container.style.display = active ? 'block' : 'none';
                    }
                    document.body.style.overflow = active ? 'hidden' : '';
                },

                setFontSize: function(fontSize) {
                    document.documentElement.style.setProperty('--nng-font-size', fontSize);
                    const container = document.getElementById('nng-chrome-reader-container');
                    if (container) {
                        container.style.setProperty('--nng-font-size', fontSize);
                    }
                }
            };
        }

        window.__nngReaderEngine.init();
        window.__nngReaderEngine.setReaderActive(" + activeStr + @");
    } catch (err) {
        console.error('[NNG Reader Mode] Distiller error:', err);
    }
})();
");
        return sb.ToString();
    }

    /// <summary>
    /// JavaScript snippet to toggle Reader Mode in the live WebView.
    /// </summary>
    public static string GetToggleReaderScript(bool active)
    {
        var activeStr = active ? "true" : "false";
        return $"if (window.__nngReaderEngine) {{ window.__nngReaderEngine.setReaderActive({activeStr}); }}";
    }

    /// <summary>
    /// JavaScript snippet to change font size in Reader Mode instantly.
    /// </summary>
    public static string GetSetFontSizeScript(string fontSize)
    {
        var safeSize = HttpUtility.JavaScriptStringEncode(fontSize);
        return $"if (window.__nngReaderEngine) {{ window.__nngReaderEngine.setFontSize('{safeSize}'); }}";
    }
}
