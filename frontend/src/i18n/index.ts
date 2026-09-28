import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import ar from './ar.json';
import en from './en.json';

/**
 * react-i18next setup (00-Frontend-Specs.md, section 10). Keys are hierarchical
 * (module.screen.field) and organized by feature, matching the spec's convention.
 */
void i18n.use(initReactI18next).init({
  resources: {
    ar: { translation: ar },
    en: { translation: en }
  },
  lng: 'ar',
  fallbackLng: 'ar',
  interpolation: { escapeValue: false }
});

export default i18n;
