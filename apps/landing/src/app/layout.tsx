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
    default: 'bugreport — баг-репорты, в которых всё на месте',
    template: '%s · bugreport',
  },
  description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
  alternates: { canonical: '/' },
  icons: {
    icon: [
      { url: '/favicon.ico' },
      { url: '/favicon-16x16.png', sizes: '16x16', type: 'image/png' },
      { url: '/favicon-32x32.png', sizes: '32x32', type: 'image/png' },
    ],
    apple: '/apple-touch-icon.png',
  },
  openGraph: {
    type: 'website',
    locale: 'ru_RU',
    url: siteUrl,
    siteName: 'bugreport',
    title: 'bugreport — баг-репорты, в которых всё на месте',
    description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
    images: [{ url: '/og-image.svg', width: 1200, height: 630, alt: 'bugreport' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'bugreport — баг-репорты, в которых всё на месте',
    description: 'Open-source инструмент для структурированных баг-репортов. Self-hosted, MIT.',
    images: ['/og-image.svg'],
  },
  robots: { index: true, follow: true },
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return <html lang="ru"><body>{children}</body></html>;
}
