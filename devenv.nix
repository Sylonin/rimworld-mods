{
  pkgs,
  lib,
  config,
  inputs,
  ...
}:

{

  packages = with pkgs; [
    git
    dotnetCorePackages.dotnet_10.sdk
    csharpier
    steamcmd
    just
    nushell
    libxml2
    oxipng
    steam-run
  ];

}
