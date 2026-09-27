[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

function Test-Endpoint($url, $name) {
    Write-Output "========================================"
    Write-Output "Analyzing $name ($url)"
    Write-Output "========================================"
    try {
        $wc = New-Object System.Net.WebClient
        $wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
        $raw = $wc.DownloadString($url)
        Write-Output "Downloaded $($raw.Length) characters."

        # Show first 1000 characters
        Write-Output "--- FIRST 600 CHARS ---"
        Write-Output $raw.Substring(0, [Math]::Min(600, $raw.Length))

        [xml]$doc = $raw

        if ($doc.urlset) {
            Write-Output "Type: URLSET (Sitemap)"
            $urls = $doc.urlset.url
            Write-Output "Total URL nodes: $($urls.Count)"
            $sample = $urls | Select-Object -First 3
            foreach ($u in $sample) {
                Write-Output "  Loc: $($u.loc)"
                if ($u.lastmod) { Write-Output "  LastMod: $($u.lastmod)" }
                if ($u.news) {
                    Write-Output "  News:Title: $($u.news.title)"
                    Write-Output "  News:PubDate: $($u.news.publication_date)"
                }
                if ($u.image) {
                    Write-Output "  Image:Loc: $($u.image.loc)"
                }
            }
        } elseif ($doc.sitemapindex) {
            Write-Output "Type: SITEMAP INDEX"
            Write-Output "Total Sub-sitemaps: $($doc.sitemapindex.sitemap.Count)"
            $doc.sitemapindex.sitemap | Select-Object -First 5 | ForEach-Object {
                Write-Output "  Sub-sitemap: $($_.loc) (lastmod: $($_.lastmod))"
            }
        } elseif ($doc.rss) {
            Write-Output "Type: RSS Feed"
            $items = $doc.rss.channel.item
            Write-Output "Total RSS items: $($items.Count)"
            $sample = $items | Select-Object -First 3
            foreach ($it in $sample) {
                Write-Output "  Title: $($it.title)"
                Write-Output "  Link: $($it.link)"
                Write-Output "  PubDate: $($it.pubDate)"
                Write-Output "  Category: $($it.category -join ', ')"
                if ($it.enclosure) { Write-Output "  Enclosure: $($it.enclosure.url)" }
                if ($it.creator) { Write-Output "  Creator: $($it.creator)" }
                Write-Output "  Description snippet: $($it.description.Substring(0, [Math]::Min(120, $it.description.Length)))"
            }
        } else {
            Write-Output "Unknown XML root: $($doc.DocumentElement.Name)"
        }
    } catch {
        Write-Output "ERROR: $($_.Exception.ToString())"
    }
}

Test-Endpoint "https://www.channelstv.com/news-sitemap.xml" "News Sitemap"
Test-Endpoint "https://www.channelstv.com/sitemap.xml" "Default Sitemap"
Test-Endpoint "https://www.channelstv.com/feed/" "RSS Feed"
