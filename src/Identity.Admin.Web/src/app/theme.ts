import type { ThemeConfig } from 'antd';

export const consoleTheme: ThemeConfig = {
  // Ant Design light theme/geometry with AA text contrast. Accent shades are
  // the official blue 7/8/9 palette; semantic text uses the corresponding dark
  // green/gold/red shades. Keep these decisions in tokens, not CSS overrides.
  cssVar: { key: 'identity-console' },
  token: {
    colorPrimary: '#0958d9',
    colorPrimaryHover: '#003eb3',
    colorPrimaryActive: '#002c8c',
    colorLink: '#0958d9',
    colorLinkHover: '#003eb3',
    colorLinkActive: '#002c8c',
    colorTextTertiary: 'rgba(0, 0, 0, 0.65)',
    colorTextDescription: 'rgba(0, 0, 0, 0.65)',
    colorTextPlaceholder: 'rgba(0, 0, 0, 0.65)',
    colorSuccessText: '#237804',
    colorWarningText: '#874d00',
    colorErrorText: '#a8071a',
    colorInfoText: '#0958d9',
  },
  components: {
    Button: {
      colorError: '#a8071a',
      colorErrorHover: '#820014',
      colorErrorActive: '#5c0011',
    },
    // Tag's status variants use colorSuccess/Warning/etc directly rather than
    // the semantic Text aliases. Scope their readable foregrounds to Tag.
    Tag: {
      colorSuccess: '#237804',
      colorWarning: '#874d00',
      colorError: '#a8071a',
      colorInfo: '#0958d9',
    },
  },
};
