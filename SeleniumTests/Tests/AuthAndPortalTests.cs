using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace SeleniumTests.Tests;

public class AuthAndPortalTests
{
    private IWebDriver? _driver;
    private WebDriverWait? _wait;

    private const string BaseUrl = "http://localhost:3000";

    [SetUp]
    public void SetUp()
    {
        var options = new ChromeOptions();
        // For CI/headless runs you can uncomment:
        // options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1400,900");

        _driver = new ChromeDriver(options);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
    }

    [TearDown]
    public void TearDown()
    {
        try { _driver?.Quit(); } catch { /* ignore */ }
        _driver?.Dispose();
    }

    [Test]
    public void Login_AsAdmin_ShouldNavigateToAdminPortal()
    {
        _driver!.Navigate().GoToUrl($"{BaseUrl}/login");

        _driver.FindElement(By.CssSelector("[data-testid='login-username']")).SendKeys("admin");
        _driver.FindElement(By.CssSelector("[data-testid='login-password']")).SendKeys("admin123");
        _driver.FindElement(By.CssSelector("[data-testid='login-submit']")).Click();

        _wait!.Until(d => d.Url.Contains("/admin") || d.Url.Contains("/portal") || d.Url.EndsWith("/"));

        // Force navigate to portal redirect route to ensure role routing is exercised
        _driver.Navigate().GoToUrl($"{BaseUrl}/portal");
        _wait.Until(d => d.Url.Contains("/admin"));

        var heading = _wait.Until(d => d.FindElement(By.CssSelector("[data-testid='admin-portal'] h1")));
        Assert.That(heading.Text, Does.Contain("Admin Portal"));
    }
}


