import { dotnet } from "./_framework/dotnet.js";

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.withApplicationArguments("start").create();

setModuleImports('main.js', {
    canvas: {
        drawPoint,
        drawLineSegment,
        drawPolygon,
    },
});

/** @type{HTMLCanvasElement} */
const BG_COLOR = "#cccccc";
const canvas = document.querySelector("#diagram-output");
const ctx = canvas.getContext('2d');
ctx.fillStyle = BG_COLOR;
ctx.fillRect(0, 0, canvas.width, canvas.height);

function drawPoint(color, radius, x, y) {
    ctx.fillStyle = color;
    ctx.strokeStyle = BG_COLOR;
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(x, y, radius, 0, 2*Math.PI);
    ctx.stroke();
    ctx.fill();
}

function drawLineSegment(color, lineWidth, x1, y1, x2, y2) {
    ctx.strokeStyle = color;
    ctx.lineWidth = lineWidth;
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
}

/** @type{number[]} coordinates - flattened x,y coordinates */
function drawPolygon(color, coordinates) {
    ctx.fillStyle = color;
    ctx.beginPath();
    for (let i = 0; i < coordinates.length; i += 2) {
        let [x, y] = coordinates.slice(i, i+2);
        if (i === 0) ctx.moveTo(x, y);
        else         ctx.lineTo(x, y);
    }
    ctx.fill();
}

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
console.log(exports.Mapgen4.RunDualMesh());
