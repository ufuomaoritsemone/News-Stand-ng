$url = "https://www.channelstv.com/2026/09/19/martinez-brace-rescues-inter-in-serie-a-top-two-clash-with-roma/"
$wc = New-Object System.Net.WebClient
$wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
$html = $wc.DownloadString($url)

Write-Output "Length: $($html.Length)"

# Check OpenGraph
$ogTitle = [regex]::Match($html, '<meta\s+property=["'']og:title["'']\s+content=["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase).Groups[1].Value
$ogImage = [regex]::Match($html, '<meta\s+property=["'']og:image["'']\s+content=["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase).Groups[1].Value
$ogDesc = [regex]::Match($html, '<meta\s+property=["'']og:description["'']\s+content=["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase).Groups[1].Value

Write-Output "og:title: $ogTitle"
Write-Output "og:image: $ogImage"
Write-Output "og:description: $ogDesc"

# Check JSON-LD
$jsonLdMatches = [regex]::Matches($html, '<script\s+type=["'']application/ld\+json["''][^>]*>(.*?)</script>', [System.Text.RegularExpressions.RegexOptions]::Singleline -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
Write-Output "JSON-LD blocks found: $($jsonLdMatches.Count)"
foreach ($m in $jsonLdMatches) {
    Write-Output "Snippet: $($m.Groups[1].Value.Substring(0, [Math]::Min(300, $m.Groups[1].Value.Length)))"
}
