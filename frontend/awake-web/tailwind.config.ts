import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        bg: {
          page:  '#0e0e0f',
          card:  '#161618',
          hover: '#1f1f22',
        },
        border: '#2a2a2e',
        text: {
          primary: '#f0ede8',
          muted:   '#6b6b72',
        },
        accent: {
          DEFAULT: '#3ddc84',
          hover:   '#2fc274',
          tint:    'rgba(61,220,132,0.12)',
        },
      },
    },
  },
  plugins: [],
} satisfies Config
