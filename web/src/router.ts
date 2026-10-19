import { createRouter, createWebHistory } from 'vue-router'
import SiteListView from './views/SiteListView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'sites', component: SiteListView },
    {
      path: '/sites/:siteId',
      name: 'site',
      component: () => import('./views/SiteDetailView.vue'),
      props: (route) => ({ siteId: Number(route.params.siteId) }),
    },
    {
      path: '/sites/:siteId/reports/new',
      name: 'report-new',
      component: () => import('./views/ReportEditView.vue'),
      props: (route) => ({ siteId: Number(route.params.siteId), reportId: null, initialDate: route.query.date ?? null }),
    },
    {
      path: '/sites/:siteId/reports/:reportId',
      name: 'report',
      component: () => import('./views/ReportEditView.vue'),
      props: (route) => ({ siteId: Number(route.params.siteId), reportId: Number(route.params.reportId), initialDate: null }),
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})
