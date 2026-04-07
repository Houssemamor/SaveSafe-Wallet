/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './src/**/*.{html,ts}',
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        'inverse-on-surface': '#f0f1f2',
        'surface-container-high': '#e7e8e9',
        'primary-fixed-dim': '#b2c5ff',
        'surface-container-lowest': '#ffffff',
        'surface-container': '#edeeef',
        'on-secondary-fixed': '#0f1c2d',
        'on-primary-fixed-variant': '#0040a2',
        'on-secondary': '#ffffff',
        'secondary': '#525f73',
        'primary-container': '#0052cc',
        'secondary-container': '#d6e3fb',
        'surface-container-low': '#f3f4f5',
        'on-primary-fixed': '#001848',
        'on-surface': '#191c1d',
        'on-tertiary': '#ffffff',
        'outline-variant': '#c3c6d6',
        'inverse-primary': '#b2c5ff',
        'on-secondary-container': '#586579',
        'tertiary-fixed-dim': '#ffb59b',
        'on-background': '#191c1d',
        'secondary-fixed-dim': '#bac7de',
        'primary': '#003d9b',
        'on-tertiary-container': '#ffc6b2',
        'inverse-surface': '#2e3132',
        'on-tertiary-fixed': '#380d00',
        'surface': '#f8f9fa',
        'tertiary': '#7b2600',
        'on-error-container': '#93000a',
        'on-primary-container': '#c4d2ff',
        'surface-bright': '#f8f9fa',
        'surface-container-highest': '#e1e3e4',
        'surface-dim': '#d9dadb',
        'on-surface-variant': '#434654',
        'error-container': '#ffdad6',
        'tertiary-container': '#a33500',
        'tertiary-fixed': '#ffdbcf',
        'on-error': '#ffffff',
        'error': '#ba1a1a',
        'on-primary': '#ffffff',
        'surface-tint': '#0c56d0',
        'secondary-fixed': '#d6e3fb',
        'surface-variant': '#e1e3e4',
        'background': '#f8f9fa',
        'outline': '#737685',
        'primary-fixed': '#dae2ff',
        'on-secondary-fixed-variant': '#3b485a',
        'on-tertiary-fixed-variant': '#812800'
      },
      borderRadius: {
        DEFAULT: '0.125rem',
        lg: '0.25rem',
        xl: '0.5rem',
        full: '0.75rem'
      },
      fontFamily: {
        headline: ['Manrope'],
        body: ['Inter'],
        label: ['Inter']
      }
    }
  },
  plugins: [],
}
