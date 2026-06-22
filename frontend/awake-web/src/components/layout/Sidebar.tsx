import { Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import {
  LayoutDashboard,
  Shield,
  FileText,
  Settings,
  Users,
  LogOut,
  Menu,
  X,
  ChevronRight,
} from 'lucide-react'
import { useState } from 'react'

const RANK_COLORS: Record<number, string> = {
  [UserRank.Guest]: 'text-text-muted bg-bg-hover',
  [UserRank.Member]: 'text-blue-400 bg-blue-400/10',
  [UserRank.Officer]: 'text-accent bg-accent/10',
  [UserRank.Colonel]: 'text-yellow-400 bg-yellow-400/10',
  [UserRank.Leader]: 'text-red-400 bg-red-400/10',
}

export function Sidebar() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const [open, setOpen] = useState(false)
  const routerState = useRouterState()
  const pathname = routerState.location.pathname

  const isColonelPlus = (user?.rank ?? 0) >= UserRank.Colonel

  const navLinks = [
    { to: '/dashboard' as const, label: t('nav.dashboard'), icon: LayoutDashboard },
    { to: '/squads' as const, label: t('nav.squads'), icon: Shield },
    { to: '/tickets' as const, label: t('nav.tickets'), icon: FileText },
  ]

  function handleLogout() {
    logout()
    void navigate({ to: '/login' })
    setOpen(false)
  }

  function isActive(path: string) {
    return pathname === path || pathname.startsWith(path + '/')
  }

  const SidebarContent = () => (
    <div className="flex flex-col h-full">
      {/* Brand */}
      <div className="px-4 py-5 border-b border-border">
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-accent shadow-[0_0_6px_#3ddc84]" />
          <span className="font-bold text-text-primary tracking-wide">Awake <span className="text-accent">[LOVE]</span></span>
        </div>
        <p className="text-xs text-text-muted mt-1">STALCRAFT</p>
      </div>

      {/* Nav */}
      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        {navLinks.map(({ to, label, icon: Icon }) => (
          <Link
            key={to}
            to={to}
            onClick={() => setOpen(false)}
            className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all group ${
              isActive(to)
                ? 'bg-accent/10 text-accent'
                : 'text-text-muted hover:text-text-primary hover:bg-bg-hover'
            }`}
          >
            <Icon size={17} className={isActive(to) ? 'text-accent' : 'text-text-muted group-hover:text-text-primary'} />
            {label}
            {isActive(to) && <ChevronRight size={14} className="ml-auto text-accent/60" />}
          </Link>
        ))}

        {isColonelPlus && (
          <>
            <div className="my-2 border-t border-border" />
            <Link
              to="/manage/users"
              onClick={() => setOpen(false)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all group ${
                isActive('/manage')
                  ? 'bg-accent/10 text-accent'
                  : 'text-text-muted hover:text-text-primary hover:bg-bg-hover'
              }`}
            >
              <Users size={17} className={isActive('/manage') ? 'text-accent' : 'text-text-muted group-hover:text-text-primary'} />
              {t('nav.manage')}
              {isActive('/manage') && <ChevronRight size={14} className="ml-auto text-accent/60" />}
            </Link>
          </>
        )}
      </nav>

      {/* User section */}
      <div className="border-t border-border p-3 space-y-2">
        <Link
          to="/settings"
          onClick={() => setOpen(false)}
          className={`flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-all group ${
            isActive('/settings')
              ? 'bg-accent/10 text-accent'
              : 'text-text-muted hover:text-text-primary hover:bg-bg-hover'
          }`}
        >
          <Settings size={15} />
          {t('nav.settings')}
        </Link>

        <div className="px-3 py-2 rounded-lg bg-bg-page border border-border">
          <div className="flex items-center justify-between gap-2 mb-1">
            <span className="text-sm font-medium text-text-primary truncate">{user?.username}</span>
            <span className={`text-[10px] px-1.5 py-0.5 rounded font-semibold shrink-0 ${RANK_COLORS[user?.rank ?? 0]}`}>
              {t(`users.ranks.${user?.rank ?? 0}`)}
            </span>
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center gap-1.5 text-xs text-text-muted hover:text-red-400 transition-colors mt-1"
          >
            <LogOut size={12} />
            {t('nav.logout')}
          </button>
        </div>
      </div>
    </div>
  )

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="hidden md:flex flex-col w-60 shrink-0 bg-bg-card border-r border-border min-h-screen sticky top-0 h-screen">
        <SidebarContent />
      </aside>

      {/* Mobile: hamburger button */}
      <button
        className="md:hidden fixed top-4 left-4 z-50 p-2 bg-bg-card border border-border rounded-lg text-text-muted hover:text-text-primary"
        onClick={() => setOpen((v) => !v)}
        aria-label="Toggle menu"
      >
        {open ? <X size={18} /> : <Menu size={18} />}
      </button>

      {/* Mobile overlay */}
      {open && (
        <div className="md:hidden fixed inset-0 z-40 flex">
          <div className="w-60 bg-bg-card border-r border-border flex flex-col h-full">
            <SidebarContent />
          </div>
          <div className="flex-1 bg-black/50" onClick={() => setOpen(false)} />
        </div>
      )}
    </>
  )
}
