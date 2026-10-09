import { api } from "../../src/services/api";
import { textResponse } from "../utils/xml";

export async function loader({ params }) {
  const requestedKey = (params.key || "").replace(/\.txt$/i, "");

  let expectedKey =
    (typeof process !== "undefined" &&
      (process.env.IndexNow__Key ||
        process.env.INDEXNOW_KEY ||
        process.env.INDEXNOW_KEY)) ||
    "3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d";

  try {
    const config = await api.getIndexNowKey();
    if (config?.key) {
      expectedKey = config.key;
    }
  } catch (err) {
    console.warn("[IndexNow Key Route] Using fallback key config:", err.message);
  }

  if (requestedKey && requestedKey !== expectedKey) {
    throw new Response("IndexNow key verification mismatch", { status: 404 });
  }

  return textResponse(expectedKey, 86400); // Cache verification key for 24h
}
