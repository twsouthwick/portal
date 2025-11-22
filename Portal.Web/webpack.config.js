const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

module.exports = (env, argv) => {
    const isDevelopment = argv.mode === 'development';

    return {
        entry: './TypeScript/app.ts',
        
        output: {
            filename: 'app.js',
            path: path.resolve(__dirname, 'wwwroot/dist'),
            clean: true,
            library: {
                type: 'module',
            },
        },

        experiments: {
            outputModule: true,
        },

        devtool: isDevelopment ? 'source-map' : false,

        resolve: {
            extensions: ['.ts', '.js'],
        },

        module: {
            rules: [
                {
                    test: /\.ts$/,
                    use: 'ts-loader',
                    exclude: /node_modules/,
                },
                {
                    test: /\.css$/,
                    use: [MiniCssExtractPlugin.loader, 'css-loader'],
                },
            ],
        },

        plugins: [
            new MiniCssExtractPlugin({
                filename: 'app.css',
            })
        ],

        optimization: {
            minimize: !isDevelopment,
        },
    };
};
