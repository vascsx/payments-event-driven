import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 30000,
  
  // Número de workers para execução paralela
  workers: process.env.CI ? 2 : undefined,
  
  // Repetir testes falhos no CI
  retries: process.env.CI ? 2 : 0,
  
  // Configuração de reporters
  reporter: [
    // Reporter padrão no console (list ou dot)
    ['list'],
    
    // JUnit XML para métricas no CircleCI
    ['junit', { 
      outputFile: 'test-results/junit.xml',
      embedAnnotationsAsProperties: true,
      includeProjectInTestName: true
    }],
    
    // HTML report - dashboard interativo
    ['html', { 
      outputFolder: 'playwright-report',
      open: 'never' // Não abre automaticamente no CI
    }],
    
    // JSON report para análise adicional (opcional)
    ['json', { 
      outputFile: 'test-results/results.json' 
    }]
  ],
  
  use: {
    baseURL: 'http://localhost:8080',
    extraHTTPHeaders: {
      'Content-Type': 'application/json'
    },
    
    // Trace apenas em falhas (útil para debugging de API)
    trace: 'retain-on-failure',
  },
});