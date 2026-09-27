[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

$wc = New-Object System.Net.WebClient
$wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")

Write-Output "=== DETAILED NEWS SITEMAP SAMPLE ==="
$newsRaw = $wc.DownloadString("https://www.channelstv.com/news-sitemap.xml")
[xml]$newsXml = $newsRaw
$u0 = $newsXml.urlset.url[0]
Write-Output "URL 0 InnerXml:"
Write-Output $u0.InnerXml

Write-Output "`n=== DETAILED POST SITEMAP SAMPLE ==="
$postRaw = $wc.DownloadString("https://www.channelstv.com/post-sitemap.xml")
[xml]$postXml = $postRaw
Write-Output "Total in post-sitemap.xml: $($postXml.urlset.url.Count)"
$p0 = $postXml.urlset.url[0]
Write-Output "URL 0 InnerXml:"
Write-Output $p0.InnerXml

Write-Output "`n=== DETAILED RSS FEED SAMPLE ==="
$feedRaw = $wc.DownloadString("https://www.channelstv.com/feed/")
[xml]$feedXml = $feedRaw
$item0 = $feedXml.rss.channel.item[0]
Write-Output "Item 0 Title: $($item0.title)"
Write-Output "Item 0 Link: $($item0.link)"
Write-Output "Item 0 PubDate: $($item0.pubDate)"
Write-Output "Item 0 Description: $($item0.description)"
Write-Output "Item 0 Categories: $(($item0.category | ForEach-Object { $_.'#text' }) -join ', ')"
Write-Output "Item 0 Content present: $([bool]$item0.encoded)"
if ($item0.content) { Write-Output "Item 0 Media/Content: $($item0.content)" }
$namespaces = @{
    media = "http://search.yahoo.com/mrss/"
    content = "http://purl.org/rss/1.0/modules/content/"
}
Write-Output "Item 0 XML:"
Write-Output $item0.OuterXml
