export function LandingFooter() {
  return (
    <footer className="border-t border-border py-8">
      <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-3 px-4 text-sm text-muted-foreground md:flex-row">
        <span>
          Awake <span className="text-accent">[LOVE]</span> · STALCRAFT
        </span>
        <span>© {new Date().getFullYear()} stalcraftclans.cc</span>
      </div>
    </footer>
  )
}
