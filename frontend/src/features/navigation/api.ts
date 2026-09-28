import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../app/api';

export interface MenuTreeNode {
  id: number;
  code: string;
  name: string;
  routeKey: string | null;
  iconKey: string | null;
  children: MenuTreeNode[];
}

/** GET /api/v1/navigation/menu — drives the Sidebar (00-System-Wide-Corrections-01.md, section 3). */
export function useMenuTree() {
  const { i18n } = useTranslation();
  return useQuery({
    queryKey: ['navigation', 'menu', i18n.language],
    queryFn: async () => (await api.get<MenuTreeNode[]>('/navigation/menu', { params: { lang: i18n.language } })).data
  });
}
