/**
 * API Reverse Proxy Resource Route for React Router
 *
 * In Docker/production environments where the Web frontend runs via `react-router-serve` on port 3000,
 * this resource route catches all `/api/*` requests (including `/api/v1/img`, `/api/v1/video-stories`,
 * `/api/v1/articles`, etc.) and transparently proxies them to the backend NewsApi service.
 */

export async function loader({ request, params }) {
  return handleProxy(request, params["*"]);
}

export async function action({ request, params }) {
  return handleProxy(request, params["*"]);
}

async function handleProxy(request, subPath) {
  const backendBase = (
    process.env.API_BASE_URL ||
    process.env.NEWS_API_URL ||
    process.env.VITE_API_URL ||
    "http://127.0.0.1:56193"
  ).replace(/\/$/, "");

  const url = new URL(request.url);
  const targetUrl = `${backendBase}/api/${subPath || ""}${url.search}`;

  const forwardHeaders = new Headers();
  for (const [key, value] of request.headers.entries()) {
    const lower = key.toLowerCase();
    if (lower !== "host" && lower !== "connection") {
      forwardHeaders.set(key, value);
    }
  }

  const fetchOptions = {
    method: request.method,
    headers: forwardHeaders,
  };

  if (request.method !== "GET" && request.method !== "HEAD") {
    fetchOptions.body = request.body;
    fetchOptions.duplex = "half";
  }

  try {
    const upstreamRes = await fetch(targetUrl, fetchOptions);

    const responseHeaders = new Headers(upstreamRes.headers);
    responseHeaders.set("Access-Control-Allow-Origin", "*");

    return new Response(upstreamRes.body, {
      status: upstreamRes.status,
      statusText: upstreamRes.statusText,
      headers: responseHeaders,
    });
  } catch (err) {
    console.error(`[API Proxy] Failed to proxy ${request.method} to ${targetUrl}:`, err.message);
    return new Response(
      JSON.stringify({ error: `Backend API unreachable at ${targetUrl}: ${err.message}` }),
      {
        status: 503,
        headers: { "Content-Type": "application/json" },
      }
    );
  }
}
