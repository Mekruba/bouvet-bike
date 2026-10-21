{
  description = "bouvet-bike: .NET backend + React frontend dev environment";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  };

  outputs =
    { self, nixpkgs, ... }:
    let
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "x86_64-darwin"
        "aarch64-darwin"
      ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});
    in
    {
      devShells = forAllSystems (
        pkgs:
        let
          # Pin the SDK in one place so DOTNET_ROOT and the shell agree.
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
          nodejs = pkgs.nodejs_24;
        in
        {
          default = pkgs.mkShell {
            packages = with pkgs; [
              # backend
              dotnet-sdk
              dotnet-ef # EF Core migrations
              csharpier # C# formatter

              # frontend
              nodejs # ships npm/npx
              pnpm

              # neovim: LSPs are enabled via vim.lsp.enable (no Mason), so they must be on PATH
              omnisharp-roslyn # "omnisharp"
              netcoredbg # nvim-dap coreclr adapter
              vtsls # "vtsls"
              vscode-langservers-extracted # eslint, html, cssls, jsonls
              prettierd # conform formatter for js/ts
            ];

            # OmniSharp and netcoredbg locate the runtime through DOTNET_ROOT.
            DOTNET_ROOT = "${dotnet-sdk}/share/dotnet";
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            DOTNET_NOLOGO = "1";

            shellHook = ''
              # `dotnet tool install -g` targets
              export PATH="$HOME/.dotnet/tools:$PATH"
            '';
          };
        }
      );

      formatter = forAllSystems (pkgs: pkgs.nixfmt);
    };
}
