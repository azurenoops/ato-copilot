/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",
    "./public/index.html",
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#eff6ff',
          100: '#dbeafe',
          500: '#3b82f6',
          600: '#2563eb',
          700: '#1d4ed8',
          800: '#1e3a5f',
          900: '#0f1d33',
        },
      },
      typography: (theme) => ({
        DEFAULT: {
          css: {
            maxWidth: 'none',
            color: theme('colors.gray.700'),
            hr: {
              borderColor: theme('colors.gray.200'),
              marginTop: '1em',
              marginBottom: '1em',
            },
            'h1, h2, h3, h4': {
              color: theme('colors.gray.900'),
              fontWeight: '600',
            },
            a: {
              color: theme('colors.blue.600'),
              textDecoration: 'none',
              '&:hover': {
                textDecoration: 'underline',
              },
            },
            code: {
              color: theme('colors.indigo.600'),
              backgroundColor: theme('colors.gray.100'),
              padding: '0.15em 0.35em',
              borderRadius: '0.25rem',
              fontWeight: '500',
            },
            'code::before': { content: '""' },
            'code::after': { content: '""' },
            'ul > li::marker': {
              color: theme('colors.gray.400'),
            },
            'ol > li::marker': {
              color: theme('colors.gray.500'),
            },
          },
        },
        sm: {
          css: {
            fontSize: '0.875rem',
            'h1': { fontSize: '1.25rem' },
            'h2': { fontSize: '1.125rem' },
            'h3': { fontSize: '1rem' },
          },
        },
      }),
    },
  },
  plugins: [
    require('@tailwindcss/typography'),
  ],
}

