import { useCallback, useState } from 'react';
import { applyTheme, currentTheme, persistTheme, type Theme } from '../stores/theme';

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(currentTheme);

  const toggle = useCallback(() => {
    const next: Theme = theme === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    persistTheme(next);
    setTheme(next);
  }, [theme]);

  return { theme, toggle };
}
