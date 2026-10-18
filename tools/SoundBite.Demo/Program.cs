using SoundBite.Demo;

if (args.Length != 3 || args[0] != "--create-demo" || args[1] != "--directory")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SoundBite.Demo -- --create-demo --directory <absolute external directory>");
    return 1;
}
try
{
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root != null && !File.Exists(Path.Combine(root.FullName, "SoundBite", "SoundBite.csproj")))
        root = root.Parent;
    if (root == null) throw new InvalidOperationException("Run this tool from its repository build.");
    var created = await DemoSetup.CreateAsync(args[2], root.FullName);
    Console.WriteLine(created ? "Demo created successfully." : "Existing demo preserved; no records or passwords changed.");
    Console.WriteLine($"Database: {Path.Combine(args[2], "soundbite-demo.db")}");
    Console.WriteLine($"Private credentials: {Path.Combine(args[2], "credentials.json")}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Demo setup failed: {exception.Message}");
    return 1;
}
