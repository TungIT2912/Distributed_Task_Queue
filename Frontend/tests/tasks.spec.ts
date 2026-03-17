import { test, expect } from '@playwright/test'

test.describe('Tasks Page', () => {
  test.beforeEach(async ({ page }) => {
    // Login first
    await page.goto('/login')
    await page.fill('[data-testid="login-username"]', 'admin')
    await page.fill('[data-testid="login-password"]', 'admin123')
    await page.click('[data-testid="login-submit"]')
    await page.goto('/tasks')
  })

  test('should display tasks page', async ({ page }) => {
    await expect(page.locator('[data-testid="tasks-page"]')).toBeVisible()
    await expect(page.locator('h1')).toContainText('Tasks')
  })

  test('should filter tasks by status', async ({ page }) => {
    await page.click('button:has-text("Pending")')
    await expect(page.locator('button:has-text("Pending")')).toHaveClass(/active/)
    
    await page.click('button:has-text("Completed")')
    await expect(page.locator('button:has-text("Completed")')).toHaveClass(/active/)
  })

  test('should display tasks table', async ({ page }) => {
    await expect(page.locator('.tasks-table')).toBeVisible()
    await expect(page.locator('th:has-text("Task ID")')).toBeVisible()
    await expect(page.locator('th:has-text("Status")')).toBeVisible()
  })
})



