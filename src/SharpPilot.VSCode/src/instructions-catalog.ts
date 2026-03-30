import { InstructionsCatalogEntry, type InstructionsFileEntry } from './instructions-catalog-entry.js';

class InstructionsCatalog {
    private readonly entries: readonly InstructionsCatalogEntry[];
    private readonly byFileName: ReadonlyMap<string, InstructionsCatalogEntry>;

    constructor(data: readonly InstructionsFileEntry[]) {
        this.entries = data.map(d => new InstructionsCatalogEntry(d));
        this.byFileName = new Map(this.entries.map(e => [e.fileName, e]));
    }

    get all(): readonly InstructionsCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    findByFileName(fileName: string): InstructionsCatalogEntry | undefined {
        return this.byFileName.get(fileName);
    }
}

export const instructionsCatalog = new InstructionsCatalog([
    { settingId: 'sharppilot.instructions.codeReview', fileName: 'code-review.instructions.md', label: 'Code Review', category: 'General' },
    { settingId: 'sharppilot.instructions.designPrinciples', fileName: 'design-principles.instructions.md', label: 'Design Principles', category: 'General' },
    { settingId: 'sharppilot.instructions.docker', fileName: 'docker.instructions.md', label: 'Docker', category: 'General', contextKeys: ['hasDocker'] },
    { settingId: 'sharppilot.instructions.graphql', fileName: 'graphql.instructions.md', label: 'GraphQL', category: 'General', contextKeys: ['hasGraphql'] },
    { settingId: 'sharppilot.instructions.restApiDesign', fileName: 'rest-api-design.instructions.md', label: 'REST API Design', category: 'General' },
    { settingId: 'sharppilot.instructions.sql', fileName: 'sql.instructions.md', label: 'SQL', category: 'General' },
    { settingId: 'sharppilot.instructions.dotnet.aspnetCore', fileName: 'dotnet-aspnetcore.instructions.md', label: 'ASP.NET Core', category: '.NET', contextKeys: ['hasAspNetCore'] },
    { settingId: 'sharppilot.instructions.dotnet.asyncAwait', fileName: 'dotnet-async-await.instructions.md', label: 'Async/Await', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.blazor', fileName: 'dotnet-blazor.instructions.md', label: 'Blazor', category: '.NET', contextKeys: ['hasBlazor'] },
    { settingId: 'sharppilot.instructions.dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: 'Coding Standards', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.core', fileName: 'dotnet-core.instructions.md', label: 'Core (DI, Logging, Config, Security)', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.dapper', fileName: 'dotnet-dapper.instructions.md', label: 'Dapper', category: '.NET', contextKeys: ['hasDapper'] },
    { settingId: 'sharppilot.instructions.dotnet.debugging', fileName: 'dotnet-debugging.instructions.md', label: 'Debugging', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.entityFrameworkCore', fileName: 'dotnet-entity-framework-core.instructions.md', label: 'Entity Framework Core', category: '.NET', contextKeys: ['hasEntityFrameworkCore'] },
    { settingId: 'sharppilot.instructions.dotnet.grpc', fileName: 'dotnet-grpc.instructions.md', label: 'gRPC', category: '.NET', contextKeys: ['hasGrpc'] },
    { settingId: 'sharppilot.instructions.dotnet.maui', fileName: 'dotnet-maui.instructions.md', label: '.NET MAUI', category: '.NET', contextKeys: ['hasMaui'] },
    { settingId: 'sharppilot.instructions.dotnet.mediatorCqrs', fileName: 'dotnet-mediator-cqrs.instructions.md', label: 'Mediator / CQRS', category: '.NET', contextKeys: ['hasMediatR'] },
    { settingId: 'sharppilot.instructions.dotnet.mongodb', fileName: 'dotnet-mongodb.instructions.md', label: 'MongoDB', category: '.NET', contextKeys: ['hasMongodb'] },
    { settingId: 'sharppilot.instructions.dotnet.mysql', fileName: 'dotnet-mysql.instructions.md', label: 'MySQL', category: '.NET', contextKeys: ['hasMysql'] },
    { settingId: 'sharppilot.instructions.dotnet.nuget', fileName: 'dotnet-nuget.instructions.md', label: 'NuGet', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.oracle', fileName: 'dotnet-oracle.instructions.md', label: 'Oracle', category: '.NET', contextKeys: ['hasOracle'] },
    { settingId: 'sharppilot.instructions.dotnet.performanceMemory', fileName: 'dotnet-performance-memory.instructions.md', label: 'Performance & Memory', category: '.NET', contextKeys: ['hasDotnet'] },
    { settingId: 'sharppilot.instructions.dotnet.postgresql', fileName: 'dotnet-postgresql.instructions.md', label: 'PostgreSQL', category: '.NET', contextKeys: ['hasPostgres'] },
    { settingId: 'sharppilot.instructions.dotnet.razor', fileName: 'dotnet-razor.instructions.md', label: 'Razor', category: '.NET', contextKeys: ['hasBlazor'] },
    { settingId: 'sharppilot.instructions.dotnet.redis', fileName: 'dotnet-redis.instructions.md', label: 'Redis', category: '.NET', contextKeys: ['hasRedis'] },
    { settingId: 'sharppilot.instructions.dotnet.signalR', fileName: 'dotnet-signalr.instructions.md', label: 'SignalR', category: '.NET', contextKeys: ['hasSignalR'] },
    { settingId: 'sharppilot.instructions.dotnet.sqlite', fileName: 'dotnet-sqlite.instructions.md', label: 'SQLite', category: '.NET', contextKeys: ['hasSqlite'] },
    { settingId: 'sharppilot.instructions.dotnet.sqlServer', fileName: 'dotnet-sql-server.instructions.md', label: 'SQL Server', category: '.NET', contextKeys: ['hasSqlServer'] },
    { settingId: 'sharppilot.instructions.dotnet.testing', fileName: 'dotnet-testing.instructions.md', label: 'Testing', category: '.NET', contextKeys: ['hasDotnetTesting'] },
    { settingId: 'sharppilot.instructions.dotnet.unity', fileName: 'dotnet-unity.instructions.md', label: 'Unity', category: '.NET', contextKeys: ['hasUnity'] },
    { settingId: 'sharppilot.instructions.dotnet.winForms', fileName: 'dotnet-winforms.instructions.md', label: 'Windows Forms', category: '.NET', contextKeys: ['hasWinForms'] },
    { settingId: 'sharppilot.instructions.dotnet.wpf', fileName: 'dotnet-wpf.instructions.md', label: 'WPF', category: '.NET', contextKeys: ['hasWpf'] },
    { settingId: 'sharppilot.instructions.dotnet.xunit', fileName: 'dotnet-xunit.instructions.md', label: 'xUnit', category: '.NET', contextKeys: ['hasXunit'] },
    { settingId: 'sharppilot.instructions.dotnet.mstest', fileName: 'dotnet-mstest.instructions.md', label: 'MSTest', category: '.NET', contextKeys: ['hasMstest'] },
    { settingId: 'sharppilot.instructions.dotnet.nunit', fileName: 'dotnet-nunit.instructions.md', label: 'NUnit', category: '.NET', contextKeys: ['hasNunit'] },
    { settingId: 'sharppilot.instructions.dotnet.csharp.codingStyle', fileName: 'dotnet-csharp.instructions.md', label: 'C#', category: '.NET', contextKeys: ['hasCsharp'] },
    { settingId: 'sharppilot.instructions.dotnet.fsharp.codingStyle', fileName: 'dotnet-fsharp.instructions.md', label: 'F#', category: '.NET', contextKeys: ['hasFsharp'] },
    { settingId: 'sharppilot.instructions.dotnet.vbnet.codingStyle', fileName: 'dotnet-vbnet.instructions.md', label: 'VB.NET', category: '.NET', contextKeys: ['hasVbnet'] },
    { settingId: 'sharppilot.instructions.git.commitFormat', fileName: 'git-commit-format.instructions.md', label: 'Commit Format', category: 'Git', contextKeys: ['hasGit'] },
    { settingId: 'sharppilot.instructions.scripting.powershell', fileName: 'scripting-powershell.instructions.md', label: 'PowerShell', category: 'Scripting', contextKeys: ['hasPowerShell'] },
    { settingId: 'sharppilot.instructions.scripting.bash', fileName: 'scripting-bash.instructions.md', label: 'Bash', category: 'Scripting', contextKeys: ['hasBash'] },
    { settingId: 'sharppilot.instructions.scripting.batch', fileName: 'scripting-batch.instructions.md', label: 'Batch (CMD)', category: 'Scripting', contextKeys: ['hasBatch'] },
    { settingId: 'sharppilot.instructions.web.angular', fileName: 'web-angular.instructions.md', label: 'Angular', category: 'Web', contextKeys: ['hasAngular'] },
    { settingId: 'sharppilot.instructions.web.css', fileName: 'web-css.instructions.md', label: 'CSS', category: 'Web', contextKeys: ['hasCss'] },
    { settingId: 'sharppilot.instructions.web.html', fileName: 'web-html.instructions.md', label: 'HTML', category: 'Web', contextKeys: ['hasHtml'] },
    { settingId: 'sharppilot.instructions.web.javascript', fileName: 'web-javascript.instructions.md', label: 'JavaScript', category: 'Web', contextKeys: ['hasJavaScript', 'hasTypeScript'] },
    { settingId: 'sharppilot.instructions.web.nextJs', fileName: 'web-nextjs.instructions.md', label: 'Next.js', category: 'Web', contextKeys: ['hasNextJs'] },
    { settingId: 'sharppilot.instructions.web.nodeJs', fileName: 'web-nodejs.instructions.md', label: 'Node.js', category: 'Web', contextKeys: ['hasNodeJs'] },
    { settingId: 'sharppilot.instructions.web.react', fileName: 'web-react.instructions.md', label: 'React', category: 'Web', contextKeys: ['hasReact'] },
    { settingId: 'sharppilot.instructions.web.svelte', fileName: 'web-svelte.instructions.md', label: 'Svelte', category: 'Web', contextKeys: ['hasSvelte'] },
    { settingId: 'sharppilot.instructions.web.typescript', fileName: 'web-typescript.instructions.md', label: 'TypeScript', category: 'Web', contextKeys: ['hasTypeScript'] },
    { settingId: 'sharppilot.instructions.web.vue', fileName: 'web-vue.instructions.md', label: 'Vue.js', category: 'Web', contextKeys: ['hasVue'] },
    { settingId: 'sharppilot.instructions.web.testing', fileName: 'web-testing.instructions.md', label: 'Testing', category: 'Web', contextKeys: ['hasWebTesting'] },
    { settingId: 'sharppilot.instructions.web.vitest', fileName: 'web-vitest.instructions.md', label: 'Vitest', category: 'Web', contextKeys: ['hasVitest'] },
    { settingId: 'sharppilot.instructions.web.jest', fileName: 'web-jest.instructions.md', label: 'Jest', category: 'Web', contextKeys: ['hasJest'] },
    { settingId: 'sharppilot.instructions.web.jasmine', fileName: 'web-jasmine.instructions.md', label: 'Jasmine', category: 'Web', contextKeys: ['hasJasmine'] },
    { settingId: 'sharppilot.instructions.web.mocha', fileName: 'web-mocha.instructions.md', label: 'Mocha', category: 'Web', contextKeys: ['hasMocha'] },
    { settingId: 'sharppilot.instructions.web.playwright', fileName: 'web-playwright.instructions.md', label: 'Playwright', category: 'Web', contextKeys: ['hasPlaywright'] },
    { settingId: 'sharppilot.instructions.web.cypress', fileName: 'web-cypress.instructions.md', label: 'Cypress', category: 'Web', contextKeys: ['hasCypress'] },
]);
