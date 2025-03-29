import { dotnet } from "./_framework/dotnet.js";

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.withApplicationArguments("start").create();

setModuleImports('main.js', {
    canvas: {
        drawPoints,
        drawLineSegments,
        drawPolygon,
    },
});

/** @type{HTMLCanvasElement} */
const BG_COLOR = "#cccccc";
const canvas = document.querySelector("#diagram-output");
const ctx = canvas.getContext('2d');
ctx.fillStyle = BG_COLOR;
ctx.fillRect(0, 0, canvas.width, canvas.height);

/** @type{number[]} coordinates - flattened x,y point coordinates */
function drawPoints(color, radius, coordinates) {
    ctx.fillStyle = color;
    ctx.strokeStyle = BG_COLOR;
    ctx.lineWidth = 2;
    for (let i = 0; i < coordinates.length; i += 2) {
        ctx.beginPath();
        ctx.arc(coordinates[i], coordinates[i+1], radius, 0, 2*Math.PI);
        ctx.stroke();
        ctx.fill();
    }
}

/** @type{number[]} coordinates - flattened x1,y1,x2,y2 point coordinates */
function drawLineSegments(color, lineWidth, coordinates) {
    ctx.strokeStyle = color;
    ctx.lineWidth = lineWidth;
    for (let i = 0; i < coordinates.length; i += 4) {
        ctx.beginPath();
        ctx.moveTo(coordinates[i], coordinates[i+1]);
        ctx.lineTo(coordinates[i+2], coordinates[i+3]);
        ctx.stroke();
    }
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
