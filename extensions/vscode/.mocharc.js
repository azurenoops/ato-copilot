module.exports = {
  require: ["ts-node/register"],
  extension: ["ts"],
  spec: "test/suite/**/*.test.ts",
  timeout: 10000,
  recursive: true,
};
