import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import './globals.css';

const siteUrl = 'https://bugget.whitesnow.tech';

export const viewport: Viewport = {
  colorScheme: 'light',
  themeColor: '#f8fafc',
};

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: {
    default: 'Bugget — баг-репорты, в которых всё на месте',
    template: '%s · Bugget',
  },
  description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
  alternates: { canonical: '/' },
  icons: { icon: '/icon.svg' },
  openGraph: {
    type: 'website',
    locale: 'ru_RU',
    url: siteUrl,
    siteName: 'Bugget',
    title: 'Bugget — баг-репорты, в которых всё на месте',
    description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
    images: [{ url: '/og-image.svg', width: 1200, height: 630, alt: 'Bugget' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'Bugget — баг-репорты, в которых всё на месте',
    description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
    images: ['/og-image.svg'],
  },
  robots: { index: true, follow: true },
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <html lang="ru"><body>{children}</body></html>;
}
