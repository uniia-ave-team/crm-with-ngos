// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://uniia-ave-team.github.io',
  base: '/crm-with-ngos',
  integrations: [
    starlight({
      title: 'CRM для громадських організацій',
      description: 'Документація CRM з відкритим кодом для громадських організацій.',
      logo: { src: './src/assets/logo.svg' },
      favicon: '/favicon.svg',
      customCss: ['./src/styles/theme.css'],
      defaultLocale: 'root',
      locales: {
        root: { label: 'Українська', lang: 'uk' },
      },
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/uniia-ave-team/crm-with-ngos' },
      ],
      editLink: {
        baseUrl: 'https://github.com/uniia-ave-team/crm-with-ngos/edit/main/docs/',
      },
      lastUpdated: true,
      sidebar: [
        { label: 'Початок роботи', items: [{ autogenerate: { directory: 'start' } }] },
        { label: 'Розгортання', items: [{ autogenerate: { directory: 'deployment' } }] },
        { label: 'Посібник користувача', items: [{ autogenerate: { directory: 'guides' } }] },
        { label: 'Довідка', items: [{ autogenerate: { directory: 'help' } }] },
        {
          label: 'Для розробників',
          collapsed: true,
          items: [
            { autogenerate: { directory: 'developers' } },
            { label: 'Довідник API', link: '/api/', attrs: { target: '_self' } },
          ],
        },
      ],
    }),
  ],
});
