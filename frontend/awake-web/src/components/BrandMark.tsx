import { cn } from '@/lib/utils'

// Единый бренд-блок «точка + Awake [LOVE]» (лендинг, логин, sidebar)
export function BrandMark({
  size = 'sm',
  className,
}: {
  size?: 'sm' | 'lg'
  className?: string
}) {
  return (
    <div className={cn('flex items-center gap-2.5', className)}>
      <div className="h-2 w-2 rounded-full bg-accent shadow-[0_0_8px_hsl(var(--accent))]" />
      <span
        className={cn(
          'font-bold text-foreground',
          size === 'lg' && 'text-2xl font-black tracking-tight',
        )}
      >
        Awake <span className="text-accent">[LOVE]</span>
      </span>
    </div>
  )
}
