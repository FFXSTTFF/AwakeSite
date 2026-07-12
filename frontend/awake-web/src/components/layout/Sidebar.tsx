import { Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/store/authStore'
import { UserRank } from '@/types/api'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import {
  LayoutDashboard,
  Shield,
  FileText,
  Settings,
  Users,
  UserCircle,
  LogOut,
  Menu,
  X,
  ChevronRight,
} from 'lucide-react'
import { useState } from 'react'
import { NotificationBell } from '@/components/layout/NotificationBell'

const RANK_CLASSES: Record<number, string> = {
  [UserRank.Guest]: 'bg-secondary text-muted-foreground border-border',
  [UserRank.Member]: 'bg-blue-400/10 text-blue-400 border-blue-400/30',
  [UserRank.Officer]: 'bg-accent/10 text-accent border-accent/30',
  [UserRank.Colonel]: 'bg-yellow-400/10 text-yellow-400 border-yellow-400/30',
  [UserRank.Leader]: 'bg-destructive/10 text-destructive border-destructive/30',
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
    { to: '/profile' as const, label: 'Профиль', icon: UserCircle },
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

  const NavLink = ({ to, label, icon: Icon }: { to: string; label: string; icon: React.ComponentType<{ size?: number; className?: string }> }) => (
    <Link
      to={to as '/dashboard'}
      onClick={() => setOpen(false)}
      className={cn(
        'flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors',
        isActive(to)
          ? 'bg-accent/10 text-accent'
          : 'text-muted-foreground hover:text-foreground hover:bg-secondary',
      )}
    >
      <Icon size={16} className={isActive(to) ? 'text-accent' : 'text-muted-foreground'} />
      {label}
      {isActive(to) && <ChevronRight size={13} className="ml-auto opacity-60" />}
    </Link>
  )

  const SidebarContent = () => (
    <div className="flex flex-col h-full">
      {/* Brand */}
      <div className="px-4 py-5">
        <div className="flex items-center gap-2.5">
          <div className="w-2 h-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
          <span className="font-bold text-foreground">
            Awake <span className="text-accent">[LOVE]</span>
          </span>
        </div>
        <p className="text-xs text-muted-foreground mt-1 pl-[18px]">STALCRAFT</p>
      </div>

      <Separator />

      {/* Nav */}
      <nav className="flex-1 px-2 py-3 space-y-0.5 overflow-y-auto">
        {navLinks.map((link) => (
          <NavLink key={link.to} {...link} />
        ))}

        {isColonelPlus && (
          <>
            <div className="py-2"><Separator /></div>
            <NavLink to="/manage/users" label={t('nav.manage')} icon={Users} />
          </>
        )}
      </nav>

      <Separator />

      {/* User section */}
      <div className="p-2 space-y-1">
        <Link
          to="/settings"
          onClick={() => setOpen(false)}
          className={cn(
            'flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors',
            isActive('/settings')
              ? 'bg-accent/10 text-accent'
              : 'text-muted-foreground hover:text-foreground hover:bg-secondary',
          )}
        >
          <Settings size={16} />
          {t('nav.settings')}
        </Link>

        <div className="mx-1 p-3 rounded-md bg-secondary border border-border space-y-2">
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-medium text-foreground truncate">{user?.username}</span>
            <div className="flex items-center gap-1.5 shrink-0">
              <NotificationBell />
              <Badge className={cn('text-[10px] px-1.5 py-0 h-5 border', RANK_CLASSES[user?.rank ?? 0])}>
                {t(`users.ranks.${user?.rank ?? 0}`)}
              </Badge>
            </div>
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={handleLogout}
            className="w-full justify-start h-7 px-1 text-xs text-muted-foreground hover:text-destructive hover:bg-destructive/10 gap-1.5"
          >
            <LogOut size={12} />
            {t('nav.logout')}
          </Button>
        </div>
      </div>
    </div>
  )

  return (
    <>
      {/* Desktop sidebar */}
      <aside className="hidden md:flex flex-col w-60 shrink-0 bg-card border-r border-border min-h-screen sticky top-0 h-screen">
        <SidebarContent />
      </aside>

      {/* Mobile hamburger */}
      <Button
        variant="outline"
        size="icon"
        className="md:hidden fixed top-4 left-4 z-50 h-8 w-8"
        onClick={() => setOpen((v) => !v)}
        aria-label="Toggle menu"
      >
        {open ? <X size={16} /> : <Menu size={16} />}
      </Button>

      {/* Mobile overlay */}
      {open && (
        <div className="md:hidden fixed inset-0 z-40 flex">
          <div className="w-60 bg-card border-r border-border flex flex-col h-full">
            <SidebarContent />
          </div>
          <div className="flex-1 bg-black/50" onClick={() => setOpen(false)} />
        </div>
      )}
    </>
  )
}
