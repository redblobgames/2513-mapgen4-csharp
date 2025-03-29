import { dotnet } from "./_framework/dotnet.js";

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.withApplicationArguments("start").create();

setModuleImports('main.js', {
    dom: {
        setInnerText: (selector, time) => document.querySelector(selector).innerText = time
    }
});

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

console.log(exports.Mapgen4.RunDualMesh());
