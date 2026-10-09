import { redirect } from "react-router";
import { api } from "../../src/services/api";

export async function loader({ params }) {
  const { id } = params;
  if (!id) {
    return redirect("/");
  }

  try {
    const article = await api.getArticleById(id);
    if (article?.slug) {
      return redirect(`/news/${article.slug}`, 301);
    }
  } catch (err) {
    console.warn(`[Share Redirect] Failed to resolve article for id '${id}':`, err.message);
  }

  return redirect(`/news/${id}`, 301);
}

export default function ShareRedirectPage() {
  return null;
}
