// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://uniia-ave-team.github.io',
  base: '/crm-with-ngos',
  integrations: [
    starlight({
      title: 'CRM для громадських організацій',
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
        { label: 'Розробка', items: [{ autogenerate: { directory: 'development' } }] },
        { label: 'Архітектура', items: [{ autogenerate: { directory: 'architecture' } }] },
        { label: 'Архітектурні рішення', items: [{ autogenerate: { directory: 'decisions' } }] },
        { label: 'Довідник API', link: '/api/', attrs: { target: '_self' } },
      ],
    }),
  ],
});
