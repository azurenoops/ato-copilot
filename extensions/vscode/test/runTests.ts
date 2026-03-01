import Mocha from "mocha";
import * as path from "path";
import * as fs from "fs";

function run(): void {
  const mocha = new Mocha({
    ui: "bdd",
    color: true,
    timeout: 10000,
  });

  const testsRoot = path.resolve(__dirname, "suite");

  const files = fs.readdirSync(testsRoot).filter((f) => f.endsWith(".test.js"));

  for (const file of files) {
    mocha.addFile(path.resolve(testsRoot, file));
  }

  mocha.run((failures) => {
    if (failures > 0) {
      console.error(`${failures} test(s) failed.`);
      process.exit(1);
    }
  });
}

run();
