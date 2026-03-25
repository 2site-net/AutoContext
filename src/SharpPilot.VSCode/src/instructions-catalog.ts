import type { ToggleEntry } from './toggle-entry.js';

export interface InstructionEntry extends ToggleEntry {
    fileName: string;
}

export const instructions: readonly InstructionEntry[] = [
    { settingId: 'sharppilot.instructions.codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { settingId: 'sharppilot.instructions.designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { settingId: 'sharppilot.instructions.docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'General' },
    { settingId: 'sharppilot.instructions.graphql', fileName: 'graphql.instructions.md', label: 'GraphQL', category: 'General' },
    { settingId: 'sharppilot.instructions.restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { settingId: 'sharppilot.instructions.sql', fileName: 'sql.instructions.md', label: 'SQL', category: 'General' },
    { settingId: 'sharppilot.instructions.dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.csharp.codingStyle', fileName: 'dotnet-csharp.instructions.md', label: 'C#', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.fsharp.codingStyle', fileName: 'dotnet-fsharp.instructions.md', label: 'F#', category: '.NET' },
    { settingId: 'sharppilot.instructions.dotnet.vbnet.codingStyle', fileName: 'dotnet-vbnet.instructions.md', label: 'VB.NET', category: '.NET' },
    { settingId: 'sharppilot.instructions.git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Git' },
    { settingId: 'sharppilot.instructions.scripting.powershell', fileName: 'scripting-powershell.instructions.md', label: 'PowerShell', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.scripting.bash', fileName: 'scripting-bash.instructions.md', label: 'Bash', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.scripting.batch', fileName: 'scripting-batch.instructions.md', label: 'Batch (CMD)', category: 'Scripting' },
    { settingId: 'sharppilot.instructions.web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.css', fileName: 'web-css.instructions.md', label: 'CSS', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.html', fileName: 'web-html.instructions.md', label: 'HTML', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.javascript', fileName: 'web-javascript.instructions.md', label: 'JavaScript', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.typescript', fileName: 'web-typescript.instructions.md', label: 'TypeScript', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web' },
    { settingId: 'sharppilot.instructions.web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web' },
];

const instructionsByFileName = new Map(instructions.map(i => [i.fileName, i]));

export function instructionByFileName(fileName: string): InstructionEntry | undefined {
    return instructionsByFileName.get(fileName);
}

export function targetPath(entry: InstructionEntry): string {
    return `.github/instructions/${entry.fileName}`;
}
