import { test, expect } from '@playwright/test'

test.describe('Workers Page', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login')
    await page.fill('[data-testid="login-username"]', 'admin')
    await page.fill('[data-testid="login-password"]', 'admin123')
    await page.click('[data-testid="login-submit"]')
    await page.goto('/workers')
  })

  test('should display workers page', async ({ page }) => {
    await expect(page.locator('[data-testid="workers-page"]')).toBeVisible()
    await expect(page.locator('h1')).toContainText('Workers')
  })

  test('should display worker cards or no workers message', async ({ page }) => {
    const workersGrid = page.locator('.workers-grid')
    await expect(workersGrid).toBeVisible()
    
    // Either shows worker cards or "no workers" message
    const hasWorkers = await page.locator('.worker-card').count()
    const hasNoWorkers = await page.locator('.no-workers').isVisible()
    
    expect(hasWorkers > 0 || hasNoWorkers).toBeTruthy()
  })
})



