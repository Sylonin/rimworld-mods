{
  pkgs,
  lib,
  config,
  inputs,
  ...
}:

{
  cachix.enable = false;

  packages = with pkgs; [
    git
    dotnetCorePackages.dotnet_9.aspnetcore
    dotnetCorePackages.dotnet_9.sdk
    csharpier
  ];

}
