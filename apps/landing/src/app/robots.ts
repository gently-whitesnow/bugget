import type { MetadataRoute } from 'next';

export const dynamic = 'force-static';

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: '*', allow: '/' },
    sitemap: 'https://bugget.whitesnow.tech/sitemap.xml',
    host: 'https://bugget.whitesnow.tech',
  };
}
