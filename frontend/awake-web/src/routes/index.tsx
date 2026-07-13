import { createFileRoute } from '@tanstack/react-router'
import { LandingNav } from '@/components/landing/LandingNav'
import { HeroSection } from '@/components/landing/HeroSection'
import { LeaderboardSection } from '@/components/landing/LeaderboardSection'
import { JoinSection } from '@/components/landing/JoinSection'
import { LandingFooter } from '@/components/landing/LandingFooter'

export const Route = createFileRoute('/')({
  component: LandingPage,
})

// Публичная витрина клана: редиректа на /login больше нет,
// авторизованные видят кнопки «В дашборд» вместо «Войти»
function LandingPage() {
  return (
    <div className="min-h-screen bg-background">
      <LandingNav />
      <main>
        <HeroSection />
        <LeaderboardSection />
        <JoinSection />
      </main>
      <LandingFooter />
    </div>
  )
}
