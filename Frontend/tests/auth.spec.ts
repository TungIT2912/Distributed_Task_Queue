import { test, expect } from '@playwright/test'

test.describe('Authentication', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/')
  })

  test('should redirect to login when not authenticated', async ({ page }) => {
    await expect(page).toHaveURL('/login')
    await expect(page.locator('h2')).toContainText('Login')
  })

  test('should show error on invalid login', async ({ page }) => {
    await page.fill('[data-testid="login-username"]', 'invalid')
    await page.fill('[data-testid="login-password"]', 'invalid')
    await page.click('[data-testid="login-submit"]')
    
    await expect(page.locator('.error-message')).toBeVisible()
  })

  test('should login successfully with valid credentials', async ({ page }) => {
    await page.fill('[data-testid="login-username"]', 'admin')
    await page.fill('[data-testid="login-password"]', 'admin123')
    await page.click('[data-testid="login-submit"]')
    
    await expect(page).toHaveURL('/')
    await expect(page.locator('[data-testid="dashboard"]')).toBeVisible()
  })

  test('should navigate to register page', async ({ page }) => {
    await page.click('text=Register here')
    await expect(page).toHaveURL('/register')
    await expect(page.locator('h2')).toContainText('Register')
  })

  test('should register new user', async ({ page }) => {
    const timestamp = Date.now()
    const username = `testuser${timestamp}`
    
    await page.goto('/register')
    await page.fill('[data-testid="register-username"]', username)
    await page.fill('[data-testid="register-email"]', `${username}@test.com`)
    await page.fill('[data-testid="register-password"]', 'testpass123')
    await page.click('[data-testid="register-submit"]')
    
    await expect(page).toHaveURL('/')
    await expect(page.locator('[data-testid="dashboard"]')).toBeVisible()
  })
})



