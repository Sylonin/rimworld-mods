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
    dotnetCorePackages.dotnet_9.sdk
    csharpier
  ];

}
