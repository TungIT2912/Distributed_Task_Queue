import { test, expect } from '@playwright/test'

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login')
    await page.fill('[data-testid="login-username"]', 'admin')
    await page.fill('[data-testid="login-password"]', 'admin123')
    await page.click('[data-testid="login-submit"]')
    await expect(page).toHaveURL('/')
  })

  test('should display dashboard with stats', async ({ page }) => {
    await expect(page.locator('[data-testid="dashboard"]')).toBeVisible()
    await expect(page.locator('text=Active Workers')).toBeVisible()
    await expect(page.locator('text=Total Tasks')).toBeVisible()
    await expect(page.locator('text=Completed')).toBeVisible()
    await expect(page.locator('text=Pending')).toBeVisible()
    await expect(page.locator('text=Failed')).toBeVisible()
  })

  test('should navigate to tasks page', async ({ page }) => {
    await page.click('text=Tasks')
    await expect(page).toHaveURL('/tasks')
    await expect(page.locator('[data-testid="tasks-page"]')).toBeVisible()
  })

  test('should navigate to workers page', async ({ page }) => {
    await page.click('text=Workers')
    await expect(page).toHaveURL('/workers')
    await expect(page.locator('[data-testid="workers-page"]')).toBeVisible()
  })

  test('should logout successfully', async ({ page }) => {
    await page.click('.logout-btn')
    await expect(page).toHaveURL('/login')
    await expect(page.locator('h2')).toContainText('Login')
  })
})



